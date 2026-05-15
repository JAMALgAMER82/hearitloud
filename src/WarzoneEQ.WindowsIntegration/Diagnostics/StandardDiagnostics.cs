using System.Runtime.Versioning;
using WarzoneEQ.WindowsIntegration.Diagnostics.Checks;
using WarzoneEQ.WindowsIntegration.EqApo;
using WarzoneEQ.WindowsIntegration.LoudnessEq;

namespace WarzoneEQ.WindowsIntegration.Diagnostics;

public static class StandardDiagnostics
{
    [SupportedOSPlatform("windows")]
    public static DiagnosticEngine ForCurrentMachine()
    {
        var locator = new RegistryEqApoLocator();
        var fs = new SystemFileSystem();
        var registry = new SystemRegistry();
        return new DiagnosticEngine(new IDiagnosticCheck[]
        {
            new EqApoInstalledCheck(locator),
            new ConfigDirWritableCheck(locator, fs),
            new MasterConfigIncludeCheck(locator, fs),
            new WarzoneConfigExistsCheck(locator, fs),
            new VstPluginsCheck(locator, fs),
            new HesuviCheck(locator, fs),
            new RebootPendingCheck(registry),
            new WindowsAudioEnhancementsCheck(),
        });
    }
}
