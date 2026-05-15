namespace WarzoneEQ.WindowsIntegration.Diagnostics;

public enum DiagnosticSeverity
{
    Ok,
    Warning,
    Error,
}

public sealed record DiagnosticResult(
    string Id,
    string Title,
    DiagnosticSeverity Severity,
    string Detail,
    string? ManualFix = null,
    Action? AutoFix = null)
{
    public bool CanAutoFix => AutoFix is not null && Severity != DiagnosticSeverity.Ok;

    public static DiagnosticResult Ok(string id, string title, string detail)
        => new(id, title, DiagnosticSeverity.Ok, detail);

    public static DiagnosticResult Warn(string id, string title, string detail, string? manualFix = null, Action? autoFix = null)
        => new(id, title, DiagnosticSeverity.Warning, detail, manualFix, autoFix);

    public static DiagnosticResult Fail(string id, string title, string detail, string? manualFix = null, Action? autoFix = null)
        => new(id, title, DiagnosticSeverity.Error, detail, manualFix, autoFix);
}
