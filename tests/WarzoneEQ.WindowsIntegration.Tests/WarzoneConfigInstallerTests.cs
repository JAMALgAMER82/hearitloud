using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Models;
using WarzoneEQ.WindowsIntegration;
using WarzoneEQ.WindowsIntegration.EqApo;
using WarzoneEQ.WindowsIntegration.Files;
using Xunit;

namespace WarzoneEQ.WindowsIntegration.Tests;

public class WarzoneConfigInstallerTests : IDisposable
{
    private readonly string _configDir;

    public WarzoneConfigInstallerTests()
    {
        _configDir = Path.Combine(Path.GetTempPath(), "warzoneeq-install-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_configDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_configDir)) Directory.Delete(_configDir, recursive: true);
    }

    private WarzoneConfigInstaller NewInstaller()
        => new(new FakeEqApoLocator(_configDir), new AtomicFileWriter());

    [Fact]
    public void Install_writes_current_txt_to_warzone_subdirectory()
    {
        var path = NewInstaller().Install(new ProfileInput(AudioMode.Competitive));
        path.Should().EndWith(Path.Combine("warzone", "current.txt"));
        File.Exists(path).Should().BeTrue();
        File.ReadAllText(path).Should().Contain("Competitive mode");
    }

    [Fact]
    public void Install_appends_include_to_existing_master_config()
    {
        var masterPath = Path.Combine(_configDir, "config.txt");
        File.WriteAllText(masterPath, "Preamp: 0 dB\n");
        NewInstaller().Install(new ProfileInput(AudioMode.Competitive));
        File.ReadAllText(masterPath).Should().Contain(@"Include: warzone\current.txt");
    }

    [Fact]
    public void Install_does_not_duplicate_existing_include()
    {
        var masterPath = Path.Combine(_configDir, "config.txt");
        File.WriteAllText(masterPath, @"Include: warzone\current.txt" + Environment.NewLine);
        NewInstaller().Install(new ProfileInput(AudioMode.Competitive));
        var occurrences = System.Text.RegularExpressions.Regex.Matches(
            File.ReadAllText(masterPath), @"Include: warzone\\current\.txt").Count;
        occurrences.Should().Be(1);
    }

    [Fact]
    public void Install_does_not_create_master_config_if_absent()
    {
        NewInstaller().Install(new ProfileInput(AudioMode.Competitive));
        File.Exists(Path.Combine(_configDir, "config.txt")).Should().BeFalse();
    }
}
