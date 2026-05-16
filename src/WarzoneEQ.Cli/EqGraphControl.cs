using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using System.Windows.Forms;
using WarzoneEQ.ConfigGenerator.Filters;

namespace WarzoneEQ.Cli;

// Owner-painted EQ frequency-response graph. Logarithmic X (20 Hz – 20 kHz)
// and linear Y in dB (default ±18 dB). Renders the cumulative magnitude curve
// of the current filter list, with each filter shown as a draggable handle.
//
// Interaction model:
//   - Click empty space          -> add a peaking filter at (freq, 0 dB)
//   - Drag a handle              -> retune that filter's (freq, gain)
//   - Right-click a handle       -> delete that filter
//   - Mouse wheel on a handle    -> nudge Q up/down
//
// Sample rate fixed at 48 kHz to match Warzone / our installer recommendation.
[SupportedOSPlatform("windows")]
public sealed class EqGraphControl : Control
{
    private readonly List<Filter> _filters = new();
    private int _draggingIndex = -1;
    private const double MinFreq = 20.0;
    private const double MaxFreq = 20000.0;
    private const double MinDb = -18.0;
    private const double MaxDb = 18.0;
    private const int HandleRadius = 8;

    public event Action? FiltersChanged;

    public EqGraphControl()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.FromArgb(18, 18, 22);
        ForeColor = Color.FromArgb(230, 230, 230);
    }

    public IReadOnlyList<Filter> Filters => _filters;

    public void SetFilters(IEnumerable<Filter> filters)
    {
        _filters.Clear();
        _filters.AddRange(filters);
        _draggingIndex = -1;
        Invalidate();
        FiltersChanged?.Invoke();
    }

    public void Clear()
    {
        _filters.Clear();
        _draggingIndex = -1;
        Invalidate();
        FiltersChanged?.Invoke();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        DrawGrid(g);
        DrawResponseCurve(g);
        DrawFilterHandles(g);
        DrawLegend(g);
    }

    private void DrawGrid(Graphics g)
    {
        using var gridPen = new Pen(Color.FromArgb(45, 45, 52), 1);
        using var gridPenBright = new Pen(Color.FromArgb(70, 70, 80), 1);
        using var textBrush = new SolidBrush(Color.FromArgb(140, 140, 150));
        var smallFont = new Font("Segoe UI", 7.5F);

        // Vertical lines at decade & octave frequencies.
        double[] freqs = { 20, 30, 50, 80, 100, 200, 300, 500, 800, 1000, 2000, 3000, 5000, 8000, 10000, 15000, 20000 };
        foreach (var f in freqs)
        {
            int x = FreqToX(f);
            bool decade = f is 20 or 100 or 1000 or 10000 or 20000;
            g.DrawLine(decade ? gridPenBright : gridPen, x, 0, x, Height);
            if (decade)
            {
                var label = f >= 1000 ? $"{f / 1000:0}k" : $"{f:0}";
                g.DrawString(label, smallFont, textBrush, x + 2, Height - 16);
            }
        }

        // Horizontal lines at 6 dB increments.
        for (int db = (int)MinDb; db <= (int)MaxDb; db += 6)
        {
            int y = DbToY(db);
            bool zero = db == 0;
            g.DrawLine(zero ? gridPenBright : gridPen, 30, y, Width, y);
            g.DrawString($"{(db > 0 ? "+" : "")}{db} dB", smallFont, textBrush, 2, y - 7);
        }
    }

    private void DrawResponseCurve(Graphics g)
    {
        if (Width < 60 || Height < 40) return;
        int samples = Math.Max(64, Width - 30);
        var points = new PointF[samples];
        for (int i = 0; i < samples; i++)
        {
            double t = i / (double)(samples - 1);
            double freq = MinFreq * Math.Pow(MaxFreq / MinFreq, t);
            double db = FilterResponse.ChainMagnitudeDb(_filters, freq);
            points[i] = new PointF(FreqToX(freq), DbToY(db));
        }
        using var curvePen = new Pen(Color.FromArgb(240, 200, 80), 2.5F);
        if (points.Length >= 2) g.DrawLines(curvePen, points);

        // Subtle fill below the curve toward the 0 dB line for visual weight.
        using var fill = new SolidBrush(Color.FromArgb(40, 240, 200, 80));
        var fillPts = new List<PointF>(points);
        fillPts.Add(new PointF(points[^1].X, DbToY(0)));
        fillPts.Add(new PointF(points[0].X, DbToY(0)));
        g.FillPolygon(fill, fillPts.ToArray());
    }

    private void DrawFilterHandles(Graphics g)
    {
        for (int i = 0; i < _filters.Count; i++)
        {
            var f = _filters[i];
            var pt = HandlePos(f);
            using var fill = new SolidBrush(i == _draggingIndex
                ? Color.FromArgb(240, 200, 80)
                : Color.FromArgb(200, 60, 100, 160));
            using var stroke = new Pen(Color.FromArgb(240, 240, 240), 2);
            var rect = new RectangleF(pt.X - HandleRadius, pt.Y - HandleRadius, HandleRadius * 2, HandleRadius * 2);
            g.FillEllipse(fill, rect);
            g.DrawEllipse(stroke, rect);
        }
    }

    private void DrawLegend(Graphics g)
    {
        using var brush = new SolidBrush(Color.FromArgb(170, 170, 180));
        var font = new Font("Segoe UI", 8F, FontStyle.Italic);
        var msg = "Click empty space → add point.   Drag → retune.   Right-click → delete.";
        var size = g.MeasureString(msg, font);
        g.DrawString(msg, font, brush, Width - size.Width - 6, 4);
    }

    private PointF HandlePos(Filter f) => new(FreqToX(f.FrequencyHz), DbToY(f.GainDb ?? 0));

    private int FreqToX(double freq)
    {
        double t = Math.Log(freq / MinFreq) / Math.Log(MaxFreq / MinFreq);
        return 30 + (int)(t * (Width - 30));
    }

    private double XToFreq(int x)
    {
        double t = (x - 30) / (double)Math.Max(1, Width - 30);
        t = Math.Clamp(t, 0, 1);
        return MinFreq * Math.Pow(MaxFreq / MinFreq, t);
    }

    private int DbToY(double db)
    {
        double t = (MaxDb - db) / (MaxDb - MinDb);
        return (int)(t * Height);
    }

    private double YToDb(int y)
    {
        double t = y / (double)Math.Max(1, Height);
        t = Math.Clamp(t, 0, 1);
        return MaxDb - t * (MaxDb - MinDb);
    }

    private int HitTestHandle(Point p)
    {
        for (int i = _filters.Count - 1; i >= 0; i--)
        {
            var h = HandlePos(_filters[i]);
            if (Math.Abs(h.X - p.X) <= HandleRadius + 2 && Math.Abs(h.Y - p.Y) <= HandleRadius + 2) return i;
        }
        return -1;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Right)
        {
            var i = HitTestHandle(e.Location);
            if (i >= 0) { _filters.RemoveAt(i); Invalidate(); FiltersChanged?.Invoke(); }
            return;
        }
        if (e.Button == MouseButtons.Left)
        {
            var i = HitTestHandle(e.Location);
            if (i >= 0) { _draggingIndex = i; Invalidate(); return; }
            // Empty-space click -> add a new peaking filter at this (freq, gain).
            double freq = SnapFreq(XToFreq(e.X));
            double gain = Math.Round(YToDb(e.Y), 1);
            _filters.Add(Filter.Peaking(freq, gain, q: 1.4));
            _draggingIndex = _filters.Count - 1;
            Invalidate();
            FiltersChanged?.Invoke();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_draggingIndex < 0 || _draggingIndex >= _filters.Count) return;
        if (e.Button != MouseButtons.Left) return;
        var f = _filters[_draggingIndex];
        double newFreq = SnapFreq(XToFreq(e.X));
        double newGain = Math.Round(YToDb(e.Y), 1);
        _filters[_draggingIndex] = f with { FrequencyHz = newFreq, GainDb = newGain };
        Invalidate();
        FiltersChanged?.Invoke();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_draggingIndex >= 0) { _draggingIndex = -1; Invalidate(); }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        var i = HitTestHandle(e.Location);
        if (i < 0) return;
        var f = _filters[i];
        double q = f.Q ?? 1.4;
        q = Math.Clamp(q * (e.Delta > 0 ? 1.2 : 1.0 / 1.2), 0.3, 12.0);
        _filters[i] = f with { Q = Math.Round(q, 2) };
        Invalidate();
        FiltersChanged?.Invoke();
    }

    private static double SnapFreq(double freq)
    {
        // Round to 2 significant figures for clean config output (1234 Hz -> 1200 Hz).
        if (freq <= 0) return MinFreq;
        double mag = Math.Pow(10, Math.Floor(Math.Log10(freq)) - 1);
        return Math.Round(freq / mag) * mag;
    }
}
