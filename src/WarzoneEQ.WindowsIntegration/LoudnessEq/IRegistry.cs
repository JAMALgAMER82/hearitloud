using System.Runtime.Versioning;

namespace WarzoneEQ.WindowsIntegration.LoudnessEq;

public interface IRegistry
{
    object? GetValue(string keyName, string valueName);
    void SetValue(string keyName, string valueName, object value, Microsoft.Win32.RegistryValueKind kind);
}

[SupportedOSPlatform("windows")]
public sealed class SystemRegistry : IRegistry
{
    public object? GetValue(string keyName, string valueName)
        => Microsoft.Win32.Registry.GetValue(keyName, valueName, null);

    public void SetValue(string keyName, string valueName, object value, Microsoft.Win32.RegistryValueKind kind)
        => Microsoft.Win32.Registry.SetValue(keyName, valueName, value, kind);
}
