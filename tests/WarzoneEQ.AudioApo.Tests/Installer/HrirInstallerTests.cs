using System.IO.Abstractions.TestingHelpers;
using FluentAssertions;
using WarzoneEQ.AudioApo.Installer;
using WarzoneEQ.AudioApo.Paths;
using Xunit;

namespace WarzoneEQ.AudioApo.Tests.Installer;

public class HrirInstallerTests
{
    private readonly EqApoPaths _paths = new(@"C:\Test\EQ");

    [Fact]
    public void Copies_source_wav_to_active_slot()
    {
        var fs = new MockFileSystem();
        var src = @"C:\hesuvi\sbx33.wav";
        fs.AddFile(src, new MockFileData(new byte[] { 1, 2, 3 }));
        var installer = new HrirInstaller(fs, _paths);
        installer.Install(src);
        fs.File.Exists(_paths.ActiveHrirWav).Should().BeTrue();
        fs.File.ReadAllBytes(_paths.ActiveHrirWav).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Overwrites_existing_active_slot()
    {
        var fs = new MockFileSystem();
        var src = @"C:\hesuvi\new.wav";
        fs.AddFile(src, new MockFileData(new byte[] { 9, 9, 9 }));
        fs.AddFile(_paths.ActiveHrirWav, new MockFileData(new byte[] { 0 }));
        new HrirInstaller(fs, _paths).Install(src);
        fs.File.ReadAllBytes(_paths.ActiveHrirWav).Should().Equal(9, 9, 9);
    }

    [Fact]
    public void Throws_on_missing_source()
    {
        var fs = new MockFileSystem();
        Assert.Throws<FileNotFoundException>(() =>
            new HrirInstaller(fs, _paths).Install(@"C:\does-not-exist.wav"));
    }
}
