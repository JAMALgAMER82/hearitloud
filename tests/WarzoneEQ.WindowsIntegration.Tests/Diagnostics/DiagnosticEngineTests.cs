using FluentAssertions;
using WarzoneEQ.WindowsIntegration.Diagnostics;
using Xunit;

namespace WarzoneEQ.WindowsIntegration.Tests.Diagnostics;

public class DiagnosticEngineTests
{
    [Fact]
    public void Run_collects_all_check_results()
    {
        var engine = new DiagnosticEngine(new IDiagnosticCheck[]
        {
            new FixedCheck(DiagnosticResult.Ok("a", "A", "ok")),
            new FixedCheck(DiagnosticResult.Warn("b", "B", "warn")),
            new FixedCheck(DiagnosticResult.Fail("c", "C", "fail")),
        });
        var report = engine.Run();
        report.OkCount.Should().Be(1);
        report.WarnCount.Should().Be(1);
        report.ErrorCount.Should().Be(1);
        report.AnyFailures.Should().BeTrue();
    }

    [Fact]
    public void Run_recovers_when_a_check_throws()
    {
        var engine = new DiagnosticEngine(new IDiagnosticCheck[]
        {
            new ThrowingCheck(),
            new FixedCheck(DiagnosticResult.Ok("ok", "OK", "ok")),
        });
        var report = engine.Run();
        report.Results.Should().HaveCount(2);
        report.ErrorCount.Should().Be(1);
        report.OkCount.Should().Be(1);
    }

    [Fact]
    public void RunAndAutoFix_invokes_autofix_then_reruns()
    {
        var fixApplied = false;
        var firstResult = DiagnosticResult.Fail("x", "X", "needs fix", autoFix: () => fixApplied = true);
        var check = new MutatingCheck(firstResult, () => DiagnosticResult.Ok("x", "X", "fixed"));

        var report = new DiagnosticEngine(new[] { (IDiagnosticCheck)check }).RunAndAutoFix();

        fixApplied.Should().BeTrue();
        report.ErrorCount.Should().Be(0);
        report.OkCount.Should().Be(1);
    }

    private sealed class FixedCheck : IDiagnosticCheck
    {
        private readonly DiagnosticResult _r;
        public FixedCheck(DiagnosticResult r) => _r = r;
        public DiagnosticResult Run() => _r;
    }

    private sealed class ThrowingCheck : IDiagnosticCheck
    {
        public DiagnosticResult Run() => throw new InvalidOperationException("boom");
    }

    // Returns a "failing" result the first time and an "ok" result on subsequent runs,
    // so we can verify RunAndAutoFix re-runs after applying the fix.
    private sealed class MutatingCheck : IDiagnosticCheck
    {
        private readonly DiagnosticResult _first;
        private readonly Func<DiagnosticResult> _afterFix;
        private bool _ran;
        public MutatingCheck(DiagnosticResult first, Func<DiagnosticResult> afterFix) { _first = first; _afterFix = afterFix; }
        public DiagnosticResult Run()
        {
            if (!_ran) { _ran = true; return _first; }
            return _afterFix();
        }
    }
}
