using System.Numerics;

namespace WarzoneEQ.ConfigGenerator.Filters;

// Frequency-response evaluation for a chain of biquad filters. Used by the
// visual EQ editor to draw the magnitude curve the user is sculpting.
//
// Coefficients computed via the RBJ Audio EQ Cookbook (Robert Bristow-Johnson,
// 2005) — the same formulas EQ APO itself uses, so what the user sees in the
// graph matches what they'll hear once Apply is clicked.
//
// Sample rate defaults to 48 kHz because that's what Warzone outputs and what
// our installer recommends Windows be configured to. At other rates the
// response shape near Nyquist will differ slightly; in the EQ-relevant range
// (20 Hz – 20 kHz) the visualization is accurate either way.
public static class FilterResponse
{
    public const double DefaultSampleRate = 48000.0;
    public static readonly double DefaultQ = 1.0 / Math.Sqrt(2); // 0.707 — Butterworth Q

    // Magnitude (dB) of a single filter at a given frequency.
    public static double MagnitudeDb(Filter f, double freqHz, double sampleRate = DefaultSampleRate)
    {
        var (b0, b1, b2, a0, a1, a2) = Coefficients(f, sampleRate);
        var omega = 2.0 * Math.PI * freqHz / sampleRate;
        var z1 = Complex.FromPolarCoordinates(1.0, -omega);      // z^-1
        var z2 = z1 * z1;                                         // z^-2
        var num = b0 + b1 * z1 + b2 * z2;
        var den = a0 + a1 * z1 + a2 * z2;
        var h = num / den;
        var mag = h.Magnitude;
        // -Inf at notch nulls; clamp the floor so the graph stays drawable.
        if (mag <= 1e-9) return -180.0;
        return 20.0 * Math.Log10(mag);
    }

    // Summed magnitude of a chain (cascaded biquads = product in linear, sum in dB).
    public static double ChainMagnitudeDb(
        IReadOnlyList<Filter> chain, double freqHz, double sampleRate = DefaultSampleRate)
    {
        double total = 0;
        for (int i = 0; i < chain.Count; i++) total += MagnitudeDb(chain[i], freqHz, sampleRate);
        return total;
    }

    // RBJ cookbook biquad coefficients. All filters return (b0, b1, b2, a0, a1, a2).
    public static (double b0, double b1, double b2, double a0, double a1, double a2)
        Coefficients(Filter f, double sampleRate)
    {
        double freq = Math.Clamp(f.FrequencyHz, 10, sampleRate / 2 - 10);
        double q = f.Q ?? DefaultQ;
        double gainDb = f.GainDb ?? 0;
        double omega = 2.0 * Math.PI * freq / sampleRate;
        double cw = Math.Cos(omega);
        double sw = Math.Sin(omega);
        double alpha = sw / (2.0 * q);

        switch (f.Type)
        {
            case FilterType.HP:
            {
                double b0 = (1 + cw) / 2;
                double b1 = -(1 + cw);
                double b2 = (1 + cw) / 2;
                double a0 = 1 + alpha;
                double a1 = -2 * cw;
                double a2 = 1 - alpha;
                return (b0, b1, b2, a0, a1, a2);
            }
            case FilterType.LP:
            {
                double b0 = (1 - cw) / 2;
                double b1 = 1 - cw;
                double b2 = (1 - cw) / 2;
                double a0 = 1 + alpha;
                double a1 = -2 * cw;
                double a2 = 1 - alpha;
                return (b0, b1, b2, a0, a1, a2);
            }
            case FilterType.PK:
            {
                double A = Math.Pow(10, gainDb / 40.0);
                double b0 = 1 + alpha * A;
                double b1 = -2 * cw;
                double b2 = 1 - alpha * A;
                double a0 = 1 + alpha / A;
                double a1 = -2 * cw;
                double a2 = 1 - alpha / A;
                return (b0, b1, b2, a0, a1, a2);
            }
            case FilterType.LS:
            {
                double A = Math.Pow(10, gainDb / 40.0);
                double sqrtA = Math.Sqrt(A);
                double b0 = A * ((A + 1) - (A - 1) * cw + 2 * sqrtA * alpha);
                double b1 = 2 * A * ((A - 1) - (A + 1) * cw);
                double b2 = A * ((A + 1) - (A - 1) * cw - 2 * sqrtA * alpha);
                double a0 = (A + 1) + (A - 1) * cw + 2 * sqrtA * alpha;
                double a1 = -2 * ((A - 1) + (A + 1) * cw);
                double a2 = (A + 1) + (A - 1) * cw - 2 * sqrtA * alpha;
                return (b0, b1, b2, a0, a1, a2);
            }
            case FilterType.HS:
            {
                double A = Math.Pow(10, gainDb / 40.0);
                double sqrtA = Math.Sqrt(A);
                double b0 = A * ((A + 1) + (A - 1) * cw + 2 * sqrtA * alpha);
                double b1 = -2 * A * ((A - 1) + (A + 1) * cw);
                double b2 = A * ((A + 1) + (A - 1) * cw - 2 * sqrtA * alpha);
                double a0 = (A + 1) - (A - 1) * cw + 2 * sqrtA * alpha;
                double a1 = 2 * ((A - 1) - (A + 1) * cw);
                double a2 = (A + 1) - (A - 1) * cw - 2 * sqrtA * alpha;
                return (b0, b1, b2, a0, a1, a2);
            }
            default:
                // Passthrough (a0=1, b0=1) — unknown filter type doesn't crash the graph.
                return (1, 0, 0, 1, 0, 0);
        }
    }
}
