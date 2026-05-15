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
    public void Install_appends_conditional_block_to_existing_master_config()
    {
        var masterPath = Path.Combine(_configDir, "config.txt");
        File.WriteAllText(masterPath, "Preamp: 0 dB\n");
        NewInstaller().Install(new ProfileInput(AudioMode.Competitive));
        var content = File.ReadAllText(masterPath);
        content.Should().Contain(WarzoneMasterConfig.BlockStartMarker);
        content.Should().Contain(WarzoneMasterConfig.BlockEndMarker);
        content.Should().Contain("If(app:cod.exe");
        content.Should().Contain("EndIf");
        content.Should().Contain(@"Include: warzone\current.txt");
        content.Should().StartWith("Preamp: 0 dB");
    }

    [Fact]
    public void Install_migrates_legacy_bare_include_to_conditional_block()
    {
        var masterPath = Path.Combine(_configDir, "config.txt");
        File.WriteAllText(masterPath, "Preamp: 0 dB\n" + @"Include: warzone\current.txt" + "\n");
        NewInstaller().Install(new ProfileInput(AudioMode.Competitive));
        var content = File.ReadAllText(masterPath);
        content.Should().Contain(WarzoneMasterConfig.BlockStartMarker);
        // The bare line is now inside the If(...) block — only one Include occurrence remains.
        var occurrences = System.Text.RegularExpressions.Regex.Matches(
            content, @"Include: warzone\\current\.txt").Count;
        occurrences.Should().Be(1);
        content.Should().NotMatch(@"^\s*Include: warzone\\current\.txt\s*$"); // not bare on its own line
    }

    [Fact]
    public void Install_is_idempotent_with_managed_block_present()
    {
        var masterPath = Path.Combine(_configDir, "config.txt");
        File.WriteAllText(masterPath, "Preamp: 0 dB\n");
        NewInstaller().Install(new ProfileInput(AudioMode.Competitive));
        var firstPass = File.ReadAllText(masterPath);

        NewInstaller().Install(new ProfileInput(AudioMode.Competitive));
        var secondPass = File.ReadAllText(masterPath);

        secondPass.Should().Be(firstPass, because: "re-installing should not churn the master config");
        System.Text.RegularExpressions.Regex.Matches(secondPass, WarzoneMasterConfig.BlockStartMarker).Count
            .Should().Be(1);
    }

    [Fact]
    public void Install_does_not_create_master_config_if_absent()
    {
        NewInstaller().Install(new ProfileInput(AudioMode.Competitive));
        File.Exists(Path.Combine(_configDir, "config.txt")).Should().BeFalse();
    }
}
