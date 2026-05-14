namespace WarzoneEQ.WindowsIntegration.Tests;

public sealed class TempDir : IDisposable
{
    public string Path { get; }
    private TempDir(string p) => Path = p;

    public static TempDir Create()
    {
        var p = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wzeq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(p);
        return new TempDir(p);
    }

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch { }
    }
}
