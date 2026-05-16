namespace WarzoneEQ.WindowsIntegration.TestSignal;

// Procedurally generates a synthetic footstep-like test signal for chain
// verification. NO recorded game audio — every sample is computed from a
// seeded noise generator + analytic envelopes, so the file is copyright-clean
// and reproducible across machines.
//
// Signal recipe (mirrors the spectrum we tune for):
//   - 5 seconds, stereo 16-bit PCM @ 48 kHz
//   - 8 transient "hits" spaced ~600 ms apart
//   - Each hit: white noise band-limited to ~2-5 kHz (the footstep band our
//     FootstepHunter chain pushes), windowed with a fast attack (~5 ms) and
//     exponential decay (~120 ms) — same envelope shape as a soft footfall
//   - Hits alternate L/R/center to exercise the lateral-channel boosts
//
// When played through an active FootstepHunter chain the user should hear
// the hits sound louder, sharper, and wider than with the chain bypassed.
public static class FootstepTestSignal
{
    public const int SampleRate = 48000;
    public const int Channels = 2;
    public const int BitsPerSample = 16;
    public const double DurationSeconds = 5.0;
    public const int HitCount = 8;

    public static byte[] BuildWav()
    {
        int totalSamples = (int)(SampleRate * DurationSeconds);
        var leftR = new float[totalSamples];
        var rightR = new float[totalSamples];

        // Seeded RNG so the test signal is identical across runs.
        var rng = new Random(0xF007);

        // Schedule the hits across the 5-second window with a small lead-in.
        double leadInSec = 0.25;
        double spacingSec = (DurationSeconds - leadInSec) / HitCount;
        for (int i = 0; i < HitCount; i++)
        {
            double startSec = leadInSec + i * spacingSec;
            int startSample = (int)(startSec * SampleRate);
            // Pan pattern: rotate around the stereo image to test our lateral
            // emphasis. -1 = full left, +1 = full right, 0 = center.
            double pan = (i % 4) switch { 0 => -0.85, 1 => 0.85, 2 => -0.4, _ => 0.4 };
            RenderHit(leftR, rightR, startSample, pan, rng);
        }

        // Normalize to -6 dBFS peak so the chain has headroom to boost.
        float peak = 0;
        for (int i = 0; i < totalSamples; i++)
        {
            peak = Math.Max(peak, Math.Abs(leftR[i]));
            peak = Math.Max(peak, Math.Abs(rightR[i]));
        }
        float scale = peak > 0 ? 0.5f / peak : 1f;
        for (int i = 0; i < totalSamples; i++)
        {
            leftR[i] *= scale;
            rightR[i] *= scale;
        }

        return EncodeWav(leftR, rightR);
    }

    // Renders one footstep hit into the buffers starting at startSample.
    // Hit = ~120 ms of band-limited noise with sharp attack + exponential decay.
    private static void RenderHit(float[] left, float[] right, int startSample, double pan, Random rng)
    {
        int hitLen = (int)(0.12 * SampleRate); // 120 ms
        int attackLen = (int)(0.005 * SampleRate); // 5 ms

        // Pan to L/R amplitudes (equal-power panning).
        double angle = (pan + 1) * Math.PI / 4;
        float amplLeft = (float)Math.Cos(angle);
        float amplRight = (float)Math.Sin(angle);

        // Simple 1-pole band-pass approximation: band-pass noise at ~3 kHz with
        // bandwidth ~3 kHz. Centered in the footstep band where we push hardest.
        double centerHz = 3000;
        double dt = 1.0 / SampleRate;
        double omega = 2 * Math.PI * centerHz * dt;
        double prevBpL = 0, prevBpH = 0;
        double prevBpInL = 0, prevBpInH = 0;
        const double r = 0.96; // pole radius — narrower band as r → 1

        for (int n = 0; n < hitLen; n++)
        {
            int idx = startSample + n;
            if (idx < 0 || idx >= left.Length) continue;

            // Envelope: linear attack to peak over attackLen, exponential decay after.
            double env;
            if (n < attackLen)
                env = (double)n / attackLen;
            else
            {
                double tDecay = (n - attackLen) / (double)SampleRate;
                env = Math.Exp(-tDecay / 0.04); // ~40 ms decay tau
            }

            // Two random values per sample (one for each side of the stereo
            // pair) so the noise is decorrelated across L/R for a wider image.
            double noiseL = (rng.NextDouble() * 2 - 1);
            double noiseR = (rng.NextDouble() * 2 - 1);

            // Band-pass via a simple resonant filter (pole pair on the unit
            // circle near 3 kHz). y[n] = 2r*cos(ω)*y[n-1] - r²*y[n-2] + x[n] - x[n-2]
            double bpL = 2 * r * Math.Cos(omega) * prevBpL - r * r * prevBpInL + noiseL;
            double bpR = 2 * r * Math.Cos(omega) * prevBpH - r * r * prevBpInH + noiseR;
            prevBpInL = prevBpL; prevBpL = bpL;
            prevBpInH = prevBpH; prevBpH = bpR;

            float sampleL = (float)(bpL * env * 0.3); // 0.3 to keep pre-normalization headroom
            float sampleR = (float)(bpR * env * 0.3);

            left[idx]  += sampleL * amplLeft;
            right[idx] += sampleR * amplRight;
        }
    }

    // RIFF/WAVE encoder — 16-bit PCM stereo, no compression.
    private static byte[] EncodeWav(float[] left, float[] right)
    {
        int samples = left.Length;
        int byteRate = SampleRate * Channels * BitsPerSample / 8;
        int blockAlign = Channels * BitsPerSample / 8;
        int dataSize = samples * blockAlign;
        int fileSize = 44 + dataSize;

        var ms = new MemoryStream(fileSize);
        var w = new BinaryWriter(ms);
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        w.Write(fileSize - 8);
        w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);                           // fmt chunk size
        w.Write((short)1);                     // PCM
        w.Write((short)Channels);
        w.Write(SampleRate);
        w.Write(byteRate);
        w.Write((short)blockAlign);
        w.Write((short)BitsPerSample);
        w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        w.Write(dataSize);

        for (int i = 0; i < samples; i++)
        {
            w.Write((short)Math.Clamp(left[i] * 32767f, -32768f, 32767f));
            w.Write((short)Math.Clamp(right[i] * 32767f, -32768f, 32767f));
        }
        w.Flush();
        return ms.ToArray();
    }

    // Writes the test WAV to a fixed path under %TEMP% and returns the path.
    // The actual playback (System.Media.SoundPlayer) lives in the CLI/GUI
    // project because SoundPlayer is in System.Windows.Extensions which we
    // don't want to pull into this otherwise-headless library.
    public static string WriteToTempFile(Action<string>? log = null)
    {
        var path = Path.Combine(Path.GetTempPath(), "hearitloud-footstep-test.wav");
        var wav = BuildWav();
        File.WriteAllBytes(path, wav);
        log?.Invoke($"[test] wrote {wav.Length / 1024} KB synthetic test signal to {path}");
        return path;
    }
}
