using FluentAssertions;
using WarzoneEQ.AudioApo.Paths;
using Xunit;

namespace WarzoneEQ.AudioApo.Tests.Paths;

public class EqApoPathsTests
{
    [Fact]
    public void Default_points_to_standard_install_dir()
    {
        EqApoPaths.Default.RootDir.Should().Be(@"C:\Program Files\EqualizerAPO");
    }

    [Fact]
    public void Subpaths_compose_under_root()
    {
        var p = new EqApoPaths(@"C:\Test\EQ");
        p.ConfigDir.Should().Be(@"C:\Test\EQ\config");
        p.MasterConfigTxt.Should().Be(@"C:\Test\EQ\config\config.txt");
        p.WarzoneDir.Should().Be(@"C:\Test\EQ\config\warzone");
        p.CurrentTxt.Should().Be(@"C:\Test\EQ\config\warzone\current.txt");
        p.ActiveHrirWav.Should().Be(@"C:\Test\EQ\config\warzone\hrir\hesuvi-active.wav");
    }
}
