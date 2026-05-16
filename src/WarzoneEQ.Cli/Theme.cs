using System.Drawing;

namespace WarzoneEQ.Cli;

// Deep-purple palette inspired by modern audio apps (Krisp, FxSound rebrand,
// Discord soundboard panels). Higher saturation than the old slate dark theme,
// with lavender accent for active states. All MainForm + tab classes pull
// colors from here so a palette tweak is one-file.
public static class Theme
{
    // Window + page backgrounds
    public static readonly Color BgRoot     = Color.FromArgb(20, 13, 36);   // very dark indigo
    public static readonly Color BgChrome   = Color.FromArgb(28, 18, 48);   // title bar / footer strip
    public static readonly Color BgPanel    = Color.FromArgb(38, 26, 68);   // log textbox, input fields

    // Card / surface elevations
    public static readonly Color CardFill   = Color.FromArgb(44, 30, 78);   // card body
    public static readonly Color CardBorder = Color.FromArgb(76, 54, 122);  // 1 px stroke around cards

    // Text
    public static readonly Color FgPrimary  = Color.FromArgb(238, 232, 255); // headings + button text
    public static readonly Color FgBody     = Color.FromArgb(214, 206, 232); // body
    public static readonly Color FgMuted    = Color.FromArgb(160, 152, 188); // captions / placeholders

    // Accents (matches the lavender / violet from the reference)
    public static readonly Color Accent     = Color.FromArgb(167, 139, 250); // primary action / highlight
    public static readonly Color AccentSoft = Color.FromArgb(124, 96, 220);  // pressed / button fill
    public static readonly Color AccentDeep = Color.FromArgb(86, 61, 168);   // button gradient end
    public static readonly Color AccentLite = Color.FromArgb(196, 181, 253); // hover

    // Status colors
    public static readonly Color Ok      = Color.FromArgb(74, 222, 128);
    public static readonly Color Warn    = Color.FromArgb(250, 204, 21);
    public static readonly Color Danger  = Color.FromArgb(248, 113, 113);

    // Convenience: per-button accents that still feel "of the palette".
    // (Used to color-code the Easy Mode action buttons.)
    public static readonly Color BtnSafe       = Color.FromArgb(86, 165, 122);  // dusty green
    public static readonly Color BtnAggressive = Color.FromArgb(216, 132, 66);  // warm amber
    public static readonly Color BtnInfo       = Color.FromArgb(99, 122, 222);  // periwinkle blue
    public static readonly Color BtnNeutral    = Color.FromArgb(80, 64, 122);   // muted purple
    public static readonly Color BtnCheat      = Color.FromArgb(168, 96, 220);  // bright purple
}
