using System.Globalization;

namespace WarzoneEQ.ConfigGenerator.Plugins;

public sealed record LoudMax : Plugin
{
    private readonly double _ceilingDb;
    private LoudMax(double ceilingDb) => _ceilingDb = ceilingDb;
    public static LoudMax Limiter(double ceilingDb) => new(ceilingDb);
    public override string ToConfigLine()
        => $"Plugin: \"LoudMax\" -ceiling {_ceilingDb.ToString("0.0", CultureInfo.InvariantCulture)}";
}
