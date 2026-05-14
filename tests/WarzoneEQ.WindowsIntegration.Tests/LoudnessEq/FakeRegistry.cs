using Microsoft.Win32;
using WarzoneEQ.WindowsIntegration.LoudnessEq;

namespace WarzoneEQ.WindowsIntegration.Tests.LoudnessEq;

internal sealed class FakeRegistry : IRegistry
{
    public Dictionary<(string Key, string Value), object> Store { get; } = new();

    public object? GetValue(string keyName, string valueName)
        => Store.TryGetValue((keyName, valueName), out var v) ? v : null;

    public void SetValue(string keyName, string valueName, object value, RegistryValueKind kind)
        => Store[(keyName, valueName)] = value;
}
