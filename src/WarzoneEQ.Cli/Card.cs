using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace WarzoneEQ.Cli;

// A rounded-corner Panel with an optional 1 px border and an optional title
// strip across the top. Used to wrap each functional section of the form so
// the page reads as a layout of "cards" instead of flat dock-fill stripes.
//
// Section header (when Title is non-null):
//   <Title>                      <- 11pt bold, FgPrimary
//   <Subtitle>                   <- 9pt italic, FgMuted
//
// Content sits below the header in CardBody (auto-padded).
[SupportedOSPlatform("windows")]
public sealed class Card : Panel
{
    public int CornerRadius { get; set; } = 14;
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? Icon { get; set; } // single emoji / glyph rendered next to the title

    // v1.10.5: REMOVED the nested _body Panel. On the user's laptop the
    // nested transparent-Panel-inside-opaque-Card stack made buttons
    // invisible AND unclickable (Plugin Control checkboxes worked because
    // they sit inside additional FlowLayoutPanel/TableLayoutPanel layers,
    // but a Button.Dock=Fill directly in the transparent Body got swallowed).
    //
    // New design: Card IS the container. Children added to Card.Controls.
    // Card.Padding leaves room for the painted title strip at the top.
    // Card.Body property returns `this` so existing call sites
    // (card.Body.Controls.Add(...)) keep working without edits.
    public Control Body => this;

    public Card()
    {
        DoubleBuffered = true;
        BackColor = Theme.CardFill;
        Padding = HasTitlePadding(hasTitle: false); // updated in OnResize once Title is set
    }

    private static Padding HasTitlePadding(bool hasTitle)
        => hasTitle ? new Padding(16, 50, 16, 14) : new Padding(16, 14, 16, 14);

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        // Padding adjusts to leave room for the painted title strip at the top.
        Padding = HasTitlePadding(!string.IsNullOrEmpty(Title));
        Invalidate();
    }

    // v1.10.3: Region clipping REMOVED. Even with handle/dimension guards
    // (v1.10.1) and solid BackColor (v1.10.2), the user's laptop kept
    // failing SetParent. Rounded corners aren't worth a crash — the cards
    // now have square corners, which is purely cosmetic. The OnPaint
    // border and header still render normally.

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);

        using (var path = RoundedRectPath(r, CornerRadius))
        using (var fill = new SolidBrush(Theme.CardFill))
        using (var border = new Pen(Theme.CardBorder, 1))
        {
            g.FillPath(fill, path);
            g.DrawPath(border, path);
        }

        if (Title is { } title)
        {
            using var titleFont = new Font("Segoe UI", 11F, FontStyle.Bold);
            using var subFont = new Font("Segoe UI", 8.5F, FontStyle.Italic);
            using var titleBrush = new SolidBrush(Theme.FgPrimary);
            using var subBrush = new SolidBrush(Theme.FgMuted);
            int x = 16;
            if (Icon is { } icon)
            {
                using var iconFont = new Font("Segoe UI Emoji", 11F);
                g.DrawString(icon, iconFont, titleBrush, x - 2, 12);
                x += 22;
            }
            g.DrawString(title, titleFont, titleBrush, x, 12);
            if (Subtitle is { } sub) g.DrawString(sub, subFont, subBrush, x, 30);
        }
    }

    private static GraphicsPath RoundedRectPath(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Region MakeRoundedRegion(int w, int h, int radius)
    {
        var rect = new Rectangle(0, 0, w, h);
        using var path = RoundedRectPath(rect, radius);
        return new Region(path);
    }
}
