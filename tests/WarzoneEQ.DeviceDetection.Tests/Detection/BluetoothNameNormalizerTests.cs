using FluentAssertions;
using WarzoneEQ.DeviceDetection.Detection;
using Xunit;

namespace WarzoneEQ.DeviceDetection.Tests.Detection;

public class BluetoothNameNormalizerTests
{
    [Theory]
    [InlineData("WH-1000XM5", "WH-1000XM5")]
    [InlineData("Sony WH-1000XM5", "WH-1000XM5")]
    [InlineData("WH-1000XM5 (L)", "WH-1000XM5")]
    [InlineData("WH-1000XM5_LE", "WH-1000XM5")]
    [InlineData("  Sony WH-1000XM5  ", "WH-1000XM5")]
    [InlineData("Arctis Nova Pro Wireless (Wired)", "Arctis Nova Pro Wireless")]
    public void Normalizes_known_variations(string input, string expected)
    {
        BluetoothNameNormalizer.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Empty_or_null_returns_empty()
    {
        BluetoothNameNormalizer.Normalize(null).Should().BeEmpty();
        BluetoothNameNormalizer.Normalize("").Should().BeEmpty();
    }
}
