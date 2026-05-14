namespace WarzoneEQ.ConfigGenerator.Plugins;

public abstract record Plugin
{
    public abstract string ToConfigLine();
}
