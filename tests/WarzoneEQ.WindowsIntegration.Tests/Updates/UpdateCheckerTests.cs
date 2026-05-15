using FluentAssertions;
using WarzoneEQ.WindowsIntegration.Updates;
using Xunit;

namespace WarzoneEQ.WindowsIntegration.Tests.Updates;

public class UpdateCheckerTests
{
    [Theory]
    [InlineData("v1.1.2", "1.1.1", true)]
    [InlineData("1.1.2", "1.1.1", true)]
    [InlineData("v1.2.0", "1.1.99", true)]
    [InlineData("v2.0.0", "1.99.99", true)]
    public void IsNewer_returns_true_when_tag_strictly_greater(string tag, string current, bool expected)
        => UpdateChecker.IsNewer(tag, current).Should().Be(expected);

    [Theory]
    [InlineData("v1.1.1", "1.1.1")]   // equal
    [InlineData("v1.1.0", "1.1.1")]   // older patch
    [InlineData("v1.0.99", "1.1.0")]  // older minor
    [InlineData("v0.99.0", "1.0.0")]  // older major
    public void IsNewer_returns_false_when_tag_not_greater(string tag, string current)
        => UpdateChecker.IsNewer(tag, current).Should().BeFalse();

    [Theory]
    [InlineData("garbage", "1.0.0")]
    [InlineData("v1.0.0", "")]
    [InlineData("", "1.0.0")]
    [InlineData("1.0", "1.0.0-beta")] // pre-release tags don't parse via Version.TryParse — safe default false
    public void IsNewer_returns_false_for_unparseable_versions(string tag, string current)
        => UpdateChecker.IsNewer(tag, current).Should().BeFalse();

    [Fact]
    public void CurrentVersion_returns_a_parseable_version_string()
    {
        var v = UpdateChecker.CurrentVersion;
        v.Should().NotBeNullOrEmpty();
        Version.TryParse(v, out _).Should().BeTrue();
    }
}
