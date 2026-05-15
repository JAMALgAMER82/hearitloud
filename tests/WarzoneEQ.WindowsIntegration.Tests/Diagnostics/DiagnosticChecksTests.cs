using FluentAssertions;
using WarzoneEQ.WindowsIntegration;
using WarzoneEQ.WindowsIntegration.Diagnostics;
using WarzoneEQ.WindowsIntegration.Diagnostics.Checks;
using WarzoneEQ.WindowsIntegration.EqApo;
using WarzoneEQ.WindowsIntegration.Tests.LoudnessEq;
using Xunit;

namespace WarzoneEQ.WindowsIntegration.Tests.Diagnostics;

public class DiagnosticChecksTests
{
    private const string FakeConfigDir = @"C:\FakeEqApo\config";
    private const string FakeInstallDir = @"C:\FakeEqApo";

    private static IEqApoLocator NotInstalled() => new StubLocator(false, FakeConfigDir);
    private static IEqApoLocator Installed() => new StubLocator(true, FakeConfigDir);

    [Fact]
    public void EqApoInstalledCheck_Fails_when_not_installed()
    {
        var r = new EqApoInstalledCheck(NotInstalled()).Run();
        r.Severity.Should().Be(DiagnosticSeverity.Error);
        r.ManualFix.Should().Contain("Equalizer APO");
    }

    [Fact]
    public void EqApoInstalledCheck_Ok_when_installed()
    {
        var r = new EqApoInstalledCheck(Installed()).Run();
        r.Severity.Should().Be(DiagnosticSeverity.Ok);
    }

    [Fact]
    public void MasterConfigIncludeCheck_Ok_when_managed_block_present()
    {
        var fs = new FakeFileSystem();
        fs.Files[Path.Combine(FakeConfigDir, "config.txt")] =
            "Preamp: 0 dB\r\n" + WarzoneMasterConfig.BuildBlock() + "\r\n";
        var r = new MasterConfigIncludeCheck(Installed(), fs).Run();
        r.Severity.Should().Be(DiagnosticSeverity.Ok);
        r.Detail.Should().Contain("pass through untouched");
    }

    [Fact]
    public void MasterConfigIncludeCheck_Warns_on_legacy_bare_include_and_upgrades_via_autofix()
    {
        var fs = new FakeFileSystem();
        var master = Path.Combine(FakeConfigDir, "config.txt");
        fs.Files[master] = "Preamp: 0 dB\r\nInclude: warzone\\current.txt\r\n";

        var r = new MasterConfigIncludeCheck(Installed(), fs).Run();
        r.Severity.Should().Be(DiagnosticSeverity.Warning);
        r.CanAutoFix.Should().BeTrue();
        r.Detail.Should().Contain("Discord");

        r.AutoFix!.Invoke();
        WarzoneMasterConfig.HasManagedBlock(fs.Files[master]).Should().BeTrue();
        WarzoneMasterConfig.HasLegacyBareInclude(fs.Files[master]).Should().BeFalse();
    }

    [Fact]
    public void MasterConfigIncludeCheck_Fails_when_chain_not_referenced_and_provides_autofix()
    {
        var fs = new FakeFileSystem();
        var master = Path.Combine(FakeConfigDir, "config.txt");
        fs.Files[master] = "# nothing here\r\n";
        var r = new MasterConfigIncludeCheck(Installed(), fs).Run();
        r.Severity.Should().Be(DiagnosticSeverity.Error);
        r.CanAutoFix.Should().BeTrue();

        r.AutoFix!.Invoke();
        WarzoneMasterConfig.HasManagedBlock(fs.Files[master]).Should().BeTrue();
    }

    [Fact]
    public void MasterConfigIncludeCheck_Fails_when_master_missing()
    {
        var fs = new FakeFileSystem();
        var r = new MasterConfigIncludeCheck(Installed(), fs).Run();
        r.Severity.Should().Be(DiagnosticSeverity.Error);
        r.CanAutoFix.Should().BeFalse();
    }

    [Fact]
    public void WarzoneConfigExistsCheck_Fails_when_missing_and_suggests_auto()
    {
        var fs = new FakeFileSystem();
        var r = new WarzoneConfigExistsCheck(Installed(), fs).Run();
        r.Severity.Should().Be(DiagnosticSeverity.Error);
        r.ManualFix.Should().Contain("--auto");
    }

    [Fact]
    public void WarzoneConfigExistsCheck_Ok_when_present()
    {
        var fs = new FakeFileSystem();
        fs.Files[Path.Combine(FakeConfigDir, "warzone", "current.txt")] = "...";
        var r = new WarzoneConfigExistsCheck(Installed(), fs).Run();
        r.Severity.Should().Be(DiagnosticSeverity.Ok);
    }

    [Fact]
    public void ConfigDirWritableCheck_Fails_when_writes_throw()
    {
        var fs = new FakeFileSystem { WritesThrow = true };
        var r = new ConfigDirWritableCheck(Installed(), fs).Run();
        r.Severity.Should().Be(DiagnosticSeverity.Error);
        r.ManualFix.Should().Contain("Administrator");
    }

    [Fact]
    public void ConfigDirWritableCheck_Ok_when_probe_succeeds()
    {
        var fs = new FakeFileSystem();
        var r = new ConfigDirWritableCheck(Installed(), fs).Run();
        r.Severity.Should().Be(DiagnosticSeverity.Ok);
    }

    [Fact]
    public void VstPluginsCheck_Warns_when_both_missing()
    {
        var fs = new FakeFileSystem();
        fs.Directories.Add(Path.Combine(FakeInstallDir, "VSTPlugins"));
        var r = new VstPluginsCheck(Installed(), fs).Run();
        r.Severity.Should().Be(DiagnosticSeverity.Warning);
        r.Detail.Should().Contain("TDR Nova");
        r.Detail.Should().Contain("LoudMax");
    }

    [Fact]
    public void VstPluginsCheck_Ok_when_both_present()
    {
        var fs = new FakeFileSystem();
        var vstDir = Path.Combine(FakeInstallDir, "VSTPlugins");
        fs.Directories.Add(vstDir);
        fs.Files[Path.Combine(vstDir, "TDR Nova.dll")] = "";
        fs.Files[Path.Combine(vstDir, "LoudMax.dll")] = "";
        var r = new VstPluginsCheck(Installed(), fs).Run();
        r.Severity.Should().Be(DiagnosticSeverity.Ok);
    }

    [Fact]
    public void HesuviCheck_Ok_when_hrir_dir_present()
    {
        var fs = new FakeFileSystem();
        fs.Directories.Add(Path.Combine(FakeConfigDir, "HeSuVi", "hrir"));
        var r = new HesuviCheck(Installed(), fs).Run();
        r.Severity.Should().Be(DiagnosticSeverity.Ok);
    }

    [Fact]
    public void HesuviCheck_Warns_when_hrir_dir_missing()
    {
        var fs = new FakeFileSystem();
        var r = new HesuviCheck(Installed(), fs).Run();
        r.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void RebootPendingCheck_Ok_when_registry_empty()
    {
        var r = new RebootPendingCheck(new FakeRegistry()).Run();
        r.Severity.Should().Be(DiagnosticSeverity.Ok);
    }

    [Fact]
    public void RebootPendingCheck_Warns_when_pending_value_present()
    {
        var reg = new FakeRegistry();
        reg.Store[(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager", "PendingFileRenameOperations")] = new[] { "foo" };
        var r = new RebootPendingCheck(reg).Run();
        r.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    private sealed class StubLocator : IEqApoLocator
    {
        private readonly bool _installed;
        private readonly string _configDir;
        public StubLocator(bool installed, string configDir) { _installed = installed; _configDir = configDir; }
        public bool IsInstalled => _installed;
        public string ConfigDirectory => _installed ? _configDir : throw new InvalidOperationException();
    }
}
