using System.Runtime.Versioning;

namespace WarzoneEQ.WindowsIntegration.EqApo;

[SupportedOSPlatform("windows")]
public sealed class RegistryEqApoLocator : IEqApoLocator
{
    private readonly string? _installPath;

    public RegistryEqApoLocator()
    {
        _installPath = Microsoft.Win32.Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\EqualizerAPO", "InstallPath", null) as string;
    }

    public bool IsInstalled => !string.IsNullOrWhiteSpace(_installPath);

    public string ConfigDirectory => IsInstalled
        ? Path.Combine(_installPath!, "config")
        : throw new InvalidOperationException("Equalizer APO is not installed.");
}

public sealed class FakeEqApoLocator : IEqApoLocator
{
    private readonly string _dir;
    public FakeEqApoLocator(string configDirectory) => _dir = configDirectory;
    public bool IsInstalled => true;
    public string ConfigDirectory => _dir;
}
