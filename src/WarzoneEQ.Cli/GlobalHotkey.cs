using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace WarzoneEQ.Cli;

// Thin wrapper around Win32 RegisterHotKey. Lets the GUI hook a global
// hotkey (active even when Warzone is in focus) that fires a callback. Used
// by v1.2 to toggle the A/B slot mid-game without alt-tabbing.
[SupportedOSPlatform("windows")]
public sealed class GlobalHotkey : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [Flags]
    public enum Mod : uint
    {
        None = 0x0000,
        Alt = 0x0001,
        Ctrl = 0x0002,
        Shift = 0x0004,
        Win = 0x0008,
        NoRepeat = 0x4000,
    }

    public const int WmHotkey = 0x0312;

    private readonly HotkeyWindow _window;
    private readonly int _id;
    private readonly Action _onPressed;
    private bool _registered;

    public GlobalHotkey(Mod modifiers, Keys key, Action onPressed)
    {
        _onPressed = onPressed;
        _window = new HotkeyWindow(HandleMessage);
        _id = Math.Abs(Environment.TickCount) % 0xBFFF + 1;
        _registered = RegisterHotKey(_window.Handle, _id, (uint)(modifiers | Mod.NoRepeat), (uint)key);
    }

    public bool IsRegistered => _registered;

    private void HandleMessage(int id) { if (id == _id) _onPressed(); }

    public void Dispose()
    {
        if (_registered) UnregisterHotKey(_window.Handle, _id);
        _window.DestroyHandle();
    }

    // NativeWindow subclass so we can receive WM_HOTKEY messages without
    // a visible top-level Form.
    private sealed class HotkeyWindow : NativeWindow
    {
        private readonly Action<int> _onHotkey;
        public HotkeyWindow(Action<int> onHotkey)
        {
            _onHotkey = onHotkey;
            CreateHandle(new CreateParams());
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey) _onHotkey((int)m.WParam);
            base.WndProc(ref m);
        }
    }
}
