namespace WarzoneEQ.WindowsIntegration.Diagnostics;

public sealed record DiagnosticReport(IReadOnlyList<DiagnosticResult> Results)
{
    public int ErrorCount => Results.Count(r => r.Severity == DiagnosticSeverity.Error);
    public int WarnCount  => Results.Count(r => r.Severity == DiagnosticSeverity.Warning);
    public int OkCount    => Results.Count(r => r.Severity == DiagnosticSeverity.Ok);
    public bool AnyFailures => ErrorCount > 0;
}

public sealed class DiagnosticEngine
{
    private readonly IReadOnlyList<IDiagnosticCheck> _checks;

    public DiagnosticEngine(IEnumerable<IDiagnosticCheck> checks)
        => _checks = checks.ToList();

    public DiagnosticReport Run()
    {
        var results = new List<DiagnosticResult>();
        foreach (var check in _checks)
        {
            try
            {
                results.Add(check.Run());
            }
            catch (Exception ex)
            {
                results.Add(DiagnosticResult.Fail(
                    check.GetType().Name,
                    check.GetType().Name,
                    $"Check crashed: {ex.GetType().Name}: {ex.Message}"));
            }
        }
        return new DiagnosticReport(results);
    }

    // Re-runs the report after applying every auto-fix, so the caller can show
    // the user what changed.
    public DiagnosticReport RunAndAutoFix()
    {
        var first = Run();
        var anyFixed = false;
        foreach (var r in first.Results)
        {
            if (r.CanAutoFix)
            {
                try { r.AutoFix!(); anyFixed = true; }
                catch { /* second pass will surface the still-failing check */ }
            }
        }
        return anyFixed ? Run() : first;
    }
}
