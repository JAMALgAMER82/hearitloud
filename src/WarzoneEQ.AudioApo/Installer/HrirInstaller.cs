using System.IO.Abstractions;
using WarzoneEQ.AudioApo.Paths;

namespace WarzoneEQ.AudioApo.Installer;

public sealed class HrirInstaller
{
    private readonly IFileSystem _fs;
    private readonly EqApoPaths _paths;

    public HrirInstaller(IFileSystem fs, EqApoPaths paths)
    {
        _fs = fs;
        _paths = paths;
    }

    public void Install(string sourceWavPath)
    {
        if (!_fs.File.Exists(sourceWavPath))
            throw new FileNotFoundException("HRIR source not found.", sourceWavPath);
        _fs.Directory.CreateDirectory(_paths.HrirDir);
        _fs.File.Copy(sourceWavPath, _paths.ActiveHrirWav, overwrite: true);
    }
}
