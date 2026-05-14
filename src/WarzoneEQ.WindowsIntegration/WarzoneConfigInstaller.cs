using WarzoneEQ.ConfigGenerator.Models;
using WarzoneEQ.WindowsIntegration.EqApo;
using WarzoneEQ.WindowsIntegration.Files;

namespace WarzoneEQ.WindowsIntegration;

public sealed class WarzoneConfigInstaller
{
    private const string IncludeLine = @"Include: warzone\current.txt";
    private readonly IEqApoLocator _locator;
    private readonly IConfigFileWriter _writer;

    public WarzoneConfigInstaller(IEqApoLocator locator, IConfigFileWriter writer)
    {
        _locator = locator;
        _writer = writer;
    }

    public string Install(ProfileInput input)
    {
        EnsureMasterConfigIncludes();
        var configText = ConfigGenerator.ConfigGenerator.Generate(input);
        var path = Path.Combine(_locator.ConfigDirectory, "warzone", "current.txt");
        _writer.Write(path, configText);
        return path;
    }

    private void EnsureMasterConfigIncludes()
    {
        var masterPath = Path.Combine(_locator.ConfigDirectory, "config.txt");
        if (!File.Exists(masterPath)) return;
        var existing = File.ReadAllText(masterPath);
        if (existing.Contains(IncludeLine, StringComparison.Ordinal)) return;
        File.AppendAllText(masterPath, Environment.NewLine + IncludeLine + Environment.NewLine);
    }
}
