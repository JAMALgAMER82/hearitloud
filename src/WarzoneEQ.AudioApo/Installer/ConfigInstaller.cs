using System.IO.Abstractions;
using WarzoneEQ.AudioApo.Paths;

namespace WarzoneEQ.AudioApo.Installer;

public sealed class ConfigInstaller
{
    private readonly IFileSystem _fs;
    private readonly EqApoPaths _paths;
    private const string IncludeLine = @"Include: warzone\current.txt";

    public ConfigInstaller(IFileSystem fs, EqApoPaths paths)
    {
        _fs = fs;
        _paths = paths;
    }

    public void Install(string configText)
    {
        EnsureDirectories();
        _fs.File.WriteAllText(_paths.CurrentTxt, configText);
        EnsureIncludeLine();
    }

    private void EnsureDirectories()
    {
        foreach (var dir in new[] { _paths.WarzoneDir, _paths.ProfilesDir,
                                    _paths.HeadphoneCorrDir, _paths.HrirDir,
                                    _paths.JsfxDir, _paths.FpsCurvesDir })
            _fs.Directory.CreateDirectory(dir);
    }

    private void EnsureIncludeLine()
    {
        var existing = _fs.File.Exists(_paths.MasterConfigTxt)
            ? _fs.File.ReadAllText(_paths.MasterConfigTxt)
            : "";
        if (existing.Contains(IncludeLine, StringComparison.OrdinalIgnoreCase)) return;
        var newContents = existing.TrimEnd() + Environment.NewLine + IncludeLine + Environment.NewLine;
        _fs.File.WriteAllText(_paths.MasterConfigTxt, newContents);
    }
}
