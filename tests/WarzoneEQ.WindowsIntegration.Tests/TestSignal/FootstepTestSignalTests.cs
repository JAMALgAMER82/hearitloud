using FluentAssertions;
using WarzoneEQ.WindowsIntegration.TestSignal;
using Xunit;

namespace WarzoneEQ.WindowsIntegration.Tests.TestSignal;

public class FootstepTestSignalTests
{
    [Fact]
    public void BuildWav_emits_a_RIFF_WAVE_header()
    {
        var bytes = FootstepTestSignal.BuildWav();
        bytes.Length.Should().BeGreaterThan(44, because: "44-byte RIFF/WAVE header + audio data");
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("RIFF");
        System.Text.Encoding.ASCII.GetString(bytes, 8, 4).Should().Be("WAVE");
        System.Text.Encoding.ASCII.GetString(bytes, 12, 4).Should().Be("fmt ");
        System.Text.Encoding.ASCII.GetString(bytes, 36, 4).Should().Be("data");
    }

    [Fact]
    public void BuildWav_declares_48kHz_stereo_16_bit_format()
    {
        var bytes = FootstepTestSignal.BuildWav();
        // fmt chunk fields after the 4-byte "fmt " marker + 4-byte size
        BitConverter.ToInt16(bytes, 20).Should().Be(1, because: "PCM format");
        BitConverter.ToInt16(bytes, 22).Should().Be(2, because: "stereo");
        BitConverter.ToInt32(bytes, 24).Should().Be(48000, because: "matches Warzone + Windows recommended rate");
        BitConverter.ToInt16(bytes, 34).Should().Be(16, because: "16-bit PCM");
    }

    [Fact]
    public void BuildWav_data_section_length_matches_expected_5_seconds()
    {
        var bytes = FootstepTestSignal.BuildWav();
        // 5 seconds * 48000 Hz * 2 channels * 2 bytes/sample = 960000 bytes
        var dataSize = BitConverter.ToInt32(bytes, 40);
        dataSize.Should().Be(960000);
        bytes.Length.Should().Be(44 + 960000);
    }

    [Fact]
    public void BuildWav_is_deterministic_across_calls()
    {
        // Seeded RNG means the test signal is reproducible — useful for
        // cross-machine A/B comparisons and for catching accidental DSP
        // regressions in CI.
        var a = FootstepTestSignal.BuildWav();
        var b = FootstepTestSignal.BuildWav();
        a.Should().Equal(b);
    }

    [Fact]
    public void BuildWav_contains_actual_audio_not_silence()
    {
        var bytes = FootstepTestSignal.BuildWav();
        // Scan the data section for non-zero samples — silence would mean the
        // generator broke and ships an empty WAV.
        bool anyNonZero = false;
        for (int i = 44; i < bytes.Length; i += 2)
        {
            if (BitConverter.ToInt16(bytes, i) != 0) { anyNonZero = true; break; }
        }
        anyNonZero.Should().BeTrue();
    }
}
