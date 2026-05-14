using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Models;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Models;

public class HeadphoneCorrectionTests
{
    [Fact]
    public void Constructs_with_a_slug()
    {
        var hc = new HeadphoneCorrection("HD600");
        hc.IncludePath.Should().Be(@"warzone\headphone-correction\HD600.txt");
    }

    [Fact]
    public void Rejects_null_or_whitespace_slug()
    {
        Assert.Throws<ArgumentException>(() => new HeadphoneCorrection(""));
        Assert.Throws<ArgumentException>(() => new HeadphoneCorrection("   "));
    }

    [Fact]
    public void None_constant_represents_no_correction()
    {
        HeadphoneCorrection.None.IncludePath.Should().BeNull();
    }
}
