using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace WarzoneEQ.Cli;

// Custom horizontal slider control. WinForms' built-in TrackBar uses the
// classic Win32 chrome that looks 15 years old; we render our own purple-filled
// pill track + dark thumb to match the modern audio-app look.
//
// Value range is integer [Minimum, Maximum] (mirrors TrackBar's API so it's a
// near drop-in replacement). Fires ValueChanged whenever the user drags or
// scrolls. Mouse-wheel = ±1 step; click on empty track = jump to that x.
[SupportedOSPlatform("windows")]
public sealed class PurpleSlider : Control
{
    public int Minimum { get; set; } = 0;
    public int Maximum { get; set; } = 100;
    public int TrackHeight { get; set; } = 8;
    public int ThumbRadius { get; set; } = 9;

    private int _value;
    public int Value
    {
        get => _value;
        set
        {
            var v = Math.Clamp(value, Minimum, Maximum);
            if (v == _value) return;
            _value = v;
            ValueChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    public event EventHandler? ValueChanged;

    private bool _dragging;

    public PurpleSlider()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        Height = 28;
        Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.CardFill);

        int trackY = (Height - TrackHeight) / 2;
        var trackRect = new Rectangle(ThumbRadius, trackY, Width - ThumbRadius * 2, TrackHeight);

        // Empty (background) track — darker translucent purple
        using (var bgBrush = new SolidBrush(Color.FromArgb(80, 60, 130)))
        using (var bgPath = RoundedRect(trackRect, TrackHeight / 2))
            g.FillPath(bgBrush, bgPath);

        // Filled portion — accent purple
        float t = Maximum > Minimum ? (_value - Minimum) / (float)(Maximum - Minimum) : 0;
        int fillW = (int)(trackRect.Width * t);
        if (fillW > 0)
        {
            var fillRect = new Rectangle(trackRect.X, trackRect.Y, fillW, trackRect.Height);
            using var fillBrush = new SolidBrush(Theme.Accent);
            using var fillPath = RoundedRect(fillRect, TrackHeight / 2);
            g.FillPath(fillBrush, fillPath);
        }

        // Thumb — slightly elevated dark circle with a thin accent ring
        int thumbX = ThumbRadius + (int)((Width - ThumbRadius * 2) * t);
        int thumbY = Height / 2;
        var thumbRect = new Rectangle(thumbX - ThumbRadius, thumbY - ThumbRadius, ThumbRadius * 2, ThumbRadius * 2);
        using (var thumbFill = new SolidBrush(Color.FromArgb(28, 18, 48)))
            g.FillEllipse(thumbFill, thumbRect);
        using (var thumbStroke = new Pen(Theme.Accent, 2))
            g.DrawEllipse(thumbStroke, thumbRect);
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        if (radius <= 0 || r.Width <= 0 || r.Height <= 0) { p.AddRectangle(r); return p; }
        int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    private void SetValueFromX(int x)
    {
        float t = (x - ThumbRadius) / (float)Math.Max(1, Width - ThumbRadius * 2);
        t = Math.Clamp(t, 0, 1);
        Value = Minimum + (int)Math.Round(t * (Maximum - Minimum));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        _dragging = true;
        SetValueFromX(e.X);
        Focus();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging) SetValueFromX(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Value += e.Delta > 0 ? 1 : -1;
    }
}
