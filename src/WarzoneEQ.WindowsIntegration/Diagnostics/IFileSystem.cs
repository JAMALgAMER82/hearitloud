namespace WarzoneEQ.WindowsIntegration.Diagnostics;

// Tiny seam so diagnostic checks can be unit-tested without touching disk.
public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    string ReadAllText(string path);
    void AppendAllText(string path, string contents);
    IEnumerable<string> EnumerateFiles(string path, string searchPattern);
    void WriteAllText(string path, string contents);
    void DeleteFile(string path);
}

public sealed class SystemFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public string ReadAllText(string path) => File.ReadAllText(path);
    public void AppendAllText(string path, string contents) => File.AppendAllText(path, contents);
    public IEnumerable<string> EnumerateFiles(string path, string searchPattern)
        => Directory.Exists(path) ? Directory.EnumerateFiles(path, searchPattern) : Array.Empty<string>();
    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);
    public void DeleteFile(string path) { if (File.Exists(path)) File.Delete(path); }
}
