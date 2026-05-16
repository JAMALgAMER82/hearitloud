using WarzoneEQ.ConfigGenerator.Models;
using WarzoneEQ.WindowsIntegration.EqApo;
using WarzoneEQ.WindowsIntegration.Files;

namespace WarzoneEQ.WindowsIntegration.AbSwitcher;

public enum AbSlot { A, B }

// Live A/B mid-game: writes two pre-generated configs to disk
// (current_A.txt and current_B.txt), then current.txt is just a one-line
// Include: that selects the active slot. Switching slots = rewriting that
// one line (~10ms file write). EQ APO hot-reloads on file change (~50ms),
// so total switch latency is ~60ms — fast enough to feel instant in-game.
public sealed class AbSwitcher
{
    private readonly IEqApoLocator _locator;
    private readonly IConfigFileWriter _writer;

    public AbSwitcher(IEqApoLocator locator, IConfigFileWriter writer)
    {
        _locator = locator;
        _writer = writer;
    }

    public string WarzoneDir => Path.Combine(_locator.ConfigDirectory, "warzone");
    public string PathFor(AbSlot slot) => Path.Combine(WarzoneDir, slot == AbSlot.A ? "current_A.txt" : "current_B.txt");
    public string SelectorPath => Path.Combine(WarzoneDir, "current.txt");
    public string ActiveSlotMarkerPath => Path.Combine(WarzoneDir, "active_slot.txt");

    // Pre-generates both configs and sets A as active. Called by the GUI when
    // the user clicks Apply (or by --auto on a fresh install).
    public void Install(ProfileInput slotA, ProfileInput slotB)
    {
        _writer.Write(PathFor(AbSlot.A), ConfigGenerator.ConfigGenerator.Generate(slotA));
        _writer.Write(PathFor(AbSlot.B), ConfigGenerator.ConfigGenerator.Generate(slotB));
        SwitchTo(AbSlot.A);
    }

    public AbSlot Active
    {
        get
        {
            if (!File.Exists(ActiveSlotMarkerPath)) return AbSlot.A;
            var content = File.ReadAllText(ActiveSlotMarkerPath).Trim();
            return content.Equals("B", StringComparison.OrdinalIgnoreCase) ? AbSlot.B : AbSlot.A;
        }
    }

    public AbSlot Toggle()
    {
        var next = Active == AbSlot.A ? AbSlot.B : AbSlot.A;
        SwitchTo(next);
        return next;
    }

    public void SwitchTo(AbSlot slot)
    {
        var fileName = slot == AbSlot.A ? "current_A.txt" : "current_B.txt";
        // selector.txt is named "current.txt" because that's what the master
        // config conditional block already includes; we keep the old path so
        // existing installs upgrade cleanly without rewriting config.txt.
        _writer.Write(SelectorPath, $"# Hear It Loud A/B selector — active slot {slot}{Environment.NewLine}Include: {fileName}{Environment.NewLine}");
        _writer.Write(ActiveSlotMarkerPath, slot.ToString());
    }
}
