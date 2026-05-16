using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace WarzoneEQ.WindowsIntegration.ProcessWatch;

// Polls the foreground window once per interval, reports whenever the
// foreground process changes (e.g. "user alt-tabbed from Chrome to Warzone").
// The polling approach is intentionally simple — Win32 has SetWinEventHook
// for change events but it requires a message pump on a specific thread,
// which is fragile across thread-affinity boundaries. A 1-second poll is
// imperceptible to users and uses ~zero CPU.
[SupportedOSPlatform("windows")]
public sealed class ForegroundWatcher : IDisposable
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public TimeSpan PollInterval { get; }
    public string? CurrentProcessName { get; private set; }
    public event Action<string?>? ForegroundChanged;

    private readonly CancellationTokenSource _cts = new();
    private readonly Task _pollLoop;

    public ForegroundWatcher(TimeSpan? pollInterval = null)
    {
        PollInterval = pollInterval ?? TimeSpan.FromSeconds(1);
        _pollLoop = Task.Run(PollAsync);
    }

    private async Task PollAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var current = ReadForegroundProcessName();
                if (!string.Equals(current, CurrentProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    CurrentProcessName = current;
                    ForegroundChanged?.Invoke(current);
                }
            }
            catch { /* swallow — polling must never bring down the host process */ }
            try { await Task.Delay(PollInterval, _cts.Token); }
            catch (OperationCanceledException) { return; }
        }
    }

    // Exposed for unit tests via the static helper; the instance just calls it.
    public static string? ReadForegroundProcessName()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;
        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0) return null;
        try
        {
            using var p = Process.GetProcessById((int)pid);
            // Process.ProcessName has no ".exe"; ProcessProfileMap normalizes too.
            return p.ProcessName + ".exe";
        }
        catch { return null; }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _pollLoop.Wait(TimeSpan.FromSeconds(2)); } catch { /* best-effort */ }
        _cts.Dispose();
    }
}
