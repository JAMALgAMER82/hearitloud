using WarzoneEQ.WindowsIntegration.Diagnostics;

namespace WarzoneEQ.WindowsIntegration.Tests.Diagnostics;

internal sealed class FakeFileSystem : IFileSystem
{
    public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool WritesThrow { get; set; }

    public bool FileExists(string path) => Files.ContainsKey(path);
    public bool DirectoryExists(string path) => Directories.Contains(path);
    public string ReadAllText(string path) => Files[path];
    public void AppendAllText(string path, string contents)
    {
        if (WritesThrow) throw new UnauthorizedAccessException("simulated denial");
        Files[path] = (Files.TryGetValue(path, out var existing) ? existing : "") + contents;
    }
    public IEnumerable<string> EnumerateFiles(string path, string searchPattern)
        => Files.Keys.Where(k => k.StartsWith(path.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)
            && (searchPattern == "*.dll" ? k.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) : true));
    public void WriteAllText(string path, string contents)
    {
        if (WritesThrow) throw new UnauthorizedAccessException("simulated denial");
        Files[path] = contents;
    }
    public void DeleteFile(string path) => Files.Remove(path);
}
