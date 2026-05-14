using System.IO.Abstractions.TestingHelpers;
using FluentAssertions;
using WarzoneEQ.AudioApo.Installer;
using WarzoneEQ.AudioApo.Paths;
using Xunit;

namespace WarzoneEQ.AudioApo.Tests.Installer;

public class ConfigInstallerTests
{
    private readonly EqApoPaths _paths = new(@"C:\Test\EQ");

    [Fact]
    public void Install_writes_current_txt_with_config_text()
    {
        var fs = new MockFileSystem();
        var installer = new ConfigInstaller(fs, _paths);
        installer.Install("Filter: ON HP Fc 80 Hz");
        fs.File.ReadAllText(_paths.CurrentTxt).Should().Be("Filter: ON HP Fc 80 Hz");
    }

    [Fact]
    public void Install_appends_Include_line_to_master_config()
    {
        var fs = new MockFileSystem();
        var installer = new ConfigInstaller(fs, _paths);
        installer.Install("anything");
        fs.File.ReadAllText(_paths.MasterConfigTxt).Should().Contain(@"Include: warzone\current.txt");
    }

    [Fact]
    public void Install_does_not_duplicate_existing_Include_line()
    {
        var fs = new MockFileSystem();
        fs.AddDirectory(_paths.ConfigDir);
        fs.AddFile(_paths.MasterConfigTxt, new MockFileData("# existing\nInclude: warzone\\current.txt\n"));
        var installer = new ConfigInstaller(fs, _paths);
        installer.Install("payload");
        var text = fs.File.ReadAllText(_paths.MasterConfigTxt);
        // Count occurrences of the Include line — must be 1
        var idx = text.IndexOf(@"Include: warzone\current.txt", StringComparison.OrdinalIgnoreCase);
        text.IndexOf(@"Include: warzone\current.txt", idx + 1, StringComparison.OrdinalIgnoreCase).Should().Be(-1);
    }

    [Fact]
    public void Install_creates_all_required_subdirectories()
    {
        var fs = new MockFileSystem();
        var installer = new ConfigInstaller(fs, _paths);
        installer.Install("x");
        fs.Directory.Exists(_paths.WarzoneDir).Should().BeTrue();
        fs.Directory.Exists(_paths.ProfilesDir).Should().BeTrue();
        fs.Directory.Exists(_paths.HeadphoneCorrDir).Should().BeTrue();
        fs.Directory.Exists(_paths.HrirDir).Should().BeTrue();
        fs.Directory.Exists(_paths.JsfxDir).Should().BeTrue();
        fs.Directory.Exists(_paths.FpsCurvesDir).Should().BeTrue();
    }
}
