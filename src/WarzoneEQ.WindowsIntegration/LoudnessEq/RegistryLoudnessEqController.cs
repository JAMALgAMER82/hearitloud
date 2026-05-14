using System.Runtime.Versioning;
using Microsoft.Win32;

namespace WarzoneEQ.WindowsIntegration.LoudnessEq;

public sealed class RegistryLoudnessEqController : ILoudnessEqController
{
    private const string PropertyKeyEnabled = "{fc52a749-4be9-4510-896e-966ba6525980},3";
    private const string PropertyKeyReleaseTime = "{fc52a749-4be9-4510-896e-966ba6525980},9";

    private readonly IRegistry _registry;

    public RegistryLoudnessEqController(IRegistry? registry = null)
        => _registry = registry ?? CreateDefaultRegistry();

    [SupportedOSPlatform("windows")]
    private static IRegistry CreateDefaultRegistryWindows() => new SystemRegistry();

    private static IRegistry CreateDefaultRegistry()
    {
        if (OperatingSystem.IsWindows()) return CreateDefaultRegistryWindows();
        throw new PlatformNotSupportedException("RegistryLoudnessEqController default backend requires Windows. Pass a custom IRegistry on non-Windows platforms.");
    }

    public LoudnessEqState? Read(string endpointGuid)
    {
        var key = FxPropertiesPath(endpointGuid);
        var enabled = _registry.GetValue(key, PropertyKeyEnabled) as int?;
        var releaseTime = _registry.GetValue(key, PropertyKeyReleaseTime) as int?;
        if (enabled is null && releaseTime is null) return null;
        return new LoudnessEqState(enabled is 1, releaseTime ?? LoudnessEqState.MinReleaseTime);
    }

    public void Write(string endpointGuid, LoudnessEqState state)
    {
        var clamped = state.Clamp();
        var key = FxPropertiesPath(endpointGuid);
        _registry.SetValue(key, PropertyKeyEnabled, clamped.Enabled ? 1 : 0, RegistryValueKind.DWord);
        _registry.SetValue(key, PropertyKeyReleaseTime, clamped.ReleaseTime, RegistryValueKind.DWord);
    }

    private static string FxPropertiesPath(string endpointGuid)
        => $@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\{endpointGuid}\FxProperties";
}
