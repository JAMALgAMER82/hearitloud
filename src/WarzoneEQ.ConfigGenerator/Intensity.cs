using WarzoneEQ.ConfigGenerator.Filters;

namespace WarzoneEQ.ConfigGenerator;

public static class Intensity
{
    public static Filter Scale(Filter filter, double intensity)
    {
        if (intensity < 0 || intensity > 1)
            throw new ArgumentOutOfRangeException(nameof(intensity), "Must be in [0, 1].");
        if (!filter.GainDb.HasValue) return filter;
        return filter.WithGain(filter.GainDb.Value * intensity);
    }

    public static IReadOnlyList<Filter> Scale(IReadOnlyList<Filter> filters, double intensity)
        => filters.Select(f => Scale(f, intensity)).ToList();
}
