using FluentAssertions;
using WarzoneEQ.WindowsIntegration.Diagnostics;
using Xunit;

namespace WarzoneEQ.WindowsIntegration.Tests.Diagnostics;

public class SelfTestTests
{
    [Fact]
    public void Run_returns_a_result_per_mode_curve_basic_combination()
    {
        // 5 modes * 3 curves * 2 basic toggles = 30
        var results = SelfTest.Run();
        results.Should().HaveCount(30);
    }

    [Fact]
    public void Run_passes_every_combination()
    {
        var failed = SelfTest.Run().Where(r => !r.Passed).ToList();
        failed.Should().BeEmpty(because: "the in-process config generator must produce valid output for every supported combination");
    }
}
