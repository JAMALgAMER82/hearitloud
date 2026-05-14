using FluentAssertions;
using WarzoneEQ.WindowsIntegration.EqApo;
using Xunit;

namespace WarzoneEQ.WindowsIntegration.Tests.EqApo;

public class FakeEqApoLocatorTests
{
    [Fact]
    public void Returns_provided_directory()
    {
        var fake = new FakeEqApoLocator(@"C:\foo\config");
        fake.IsInstalled.Should().BeTrue();
        fake.ConfigDirectory.Should().Be(@"C:\foo\config");
    }
}
