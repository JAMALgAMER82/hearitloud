using WarzoneEQ.ConfigGenerator.Models;

namespace WarzoneEQ.ConfigGenerator.Filters;

public static class FpsCurves
{
    public static IReadOnlyList<Filter> Get(FpsCurveName name) => name switch
    {
        FpsCurveName.Minimalist => Minimalist,
        FpsCurveName.Moderate   => Moderate,
        FpsCurveName.Aggressive => Aggressive,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private static readonly IReadOnlyList<Filter> Minimalist = new[]
    {
        Filter.HighPass(120),
        Filter.Peaking(3000, 4, 1.2),
        Filter.HighShelf(8000, -2),
    };

    private static readonly IReadOnlyList<Filter> Moderate = new[]
    {
        Filter.LowShelf(250, -6),
        Filter.Peaking(800, -3, 4),
        Filter.Peaking(2000, 3, 1.4),
        Filter.Peaking(3500, 5, 1.8),
        Filter.Peaking(5000, 2, 1.2),
        Filter.HighShelf(10000, -3),
    };

    private static readonly IReadOnlyList<Filter> Aggressive = new[]
    {
        Filter.HighPass(80),
        Filter.Peaking(180, -4, 2),
        Filter.Peaking(500, -2, 3),
        Filter.Peaking(1200, -3, 5),
        Filter.Peaking(2800, 6, 2),
        Filter.Peaking(4000, 4, 2),
        Filter.Peaking(6000, -5, 4),
        Filter.HighShelf(7000, 1),
        Filter.Peaking(12000, -2, 1),
        Filter.LowPass(16000),
    };
}
