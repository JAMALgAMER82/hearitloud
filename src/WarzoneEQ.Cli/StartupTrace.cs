namespace WarzoneEQ.Cli;

// Writes a timestamped line to %APPDATA%\HearItLoud\startup.log BEFORE
// every risky construction step in the GUI startup sequence. If the next
// step crashes, the last logged line tells us exactly where — no guessing
// required. Append + flush per write so even a hard process kill preserves
// the trace.
public static class StartupTrace
{
    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "HearItLoud", "startup.log");

    public static void Reset()
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(Path)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path,
                $"=== Startup {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===" + Environment.NewLine +
                $"Version: {typeof(StartupTrace).Assembly.GetName().Version?.ToString(3) ?? "?"}" + Environment.NewLine +
                $"OS:      {Environment.OSVersion}" + Environment.NewLine +
                Environment.NewLine);
        }
        catch { /* startup log is best-effort */ }
    }

    public static void Step(string what)
    {
        try { File.AppendAllText(Path, $"{DateTime.Now:HH:mm:ss.fff}  {what}" + Environment.NewLine); }
        catch { /* swallow */ }
    }
}
