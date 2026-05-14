using FluentAssertions;
using WarzoneEQ.WindowsIntegration.Files;
using Xunit;

namespace WarzoneEQ.WindowsIntegration.Tests.Files;

public class AtomicFileWriterTests : IDisposable
{
    private readonly string _root;
    public AtomicFileWriterTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "warzoneeq-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Writes_new_file_with_contents()
    {
        var path = Path.Combine(_root, "out.txt");
        new AtomicFileWriter().Write(path, "hello");
        File.ReadAllText(path).Should().Be("hello");
    }

    [Fact]
    public void Overwrites_existing_file()
    {
        var path = Path.Combine(_root, "out.txt");
        File.WriteAllText(path, "original");
        new AtomicFileWriter().Write(path, "new contents");
        File.ReadAllText(path).Should().Be("new contents");
    }

    [Fact]
    public void Creates_intermediate_directories()
    {
        var path = Path.Combine(_root, "a", "b", "c", "out.txt");
        new AtomicFileWriter().Write(path, "hello");
        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void Does_not_leave_tmp_file_behind()
    {
        var path = Path.Combine(_root, "out.txt");
        new AtomicFileWriter().Write(path, "x");
        File.Exists(path + ".tmp").Should().BeFalse();
    }
}
