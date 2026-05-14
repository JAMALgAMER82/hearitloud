namespace WarzoneEQ.WindowsIntegration.EqApo;

public interface IEqApoLocator
{
    bool IsInstalled { get; }
    string ConfigDirectory { get; }
}
