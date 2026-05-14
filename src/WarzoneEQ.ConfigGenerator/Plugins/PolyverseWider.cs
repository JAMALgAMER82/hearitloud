namespace WarzoneEQ.ConfigGenerator.Plugins;

public sealed record PolyverseWider : Plugin
{
    private readonly int _width;
    private PolyverseWider(int width) => _width = width;
    public static PolyverseWider Width(int width)
    {
        if (width < 0 || width > 200)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be in [0, 200].");
        return new PolyverseWider(width);
    }
    public override string ToConfigLine() => $"Plugin: \"Polyverse Wider\" -width {_width}";
}
