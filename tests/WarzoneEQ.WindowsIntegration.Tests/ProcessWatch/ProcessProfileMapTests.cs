using FluentAssertions;
using WarzoneEQ.WindowsIntegration.AbSwitcher;
using WarzoneEQ.WindowsIntegration.ProcessWatch;
using Xunit;

namespace WarzoneEQ.WindowsIntegration.Tests.ProcessWatch;

public class ProcessProfileMapTests
{
    [Fact]
    public void Default_set_covers_known_cod_executables()
    {
        ProcessProfileMap.Default.IsGame("cod.exe").Should().BeTrue();
        ProcessProfileMap.Default.IsGame("ModernWarfare.exe").Should().BeTrue();
        ProcessProfileMap.Default.IsGame("Warzone.exe").Should().BeTrue();
        ProcessProfileMap.Default.IsGame("BlackOps6.exe").Should().BeTrue();
    }

    [Fact]
    public void Default_set_is_case_insensitive()
    {
        ProcessProfileMap.Default.IsGame("COD.EXE").Should().BeTrue();
        ProcessProfileMap.Default.IsGame("warzone.exe").Should().BeTrue();
    }

    [Fact]
    public void SlotFor_returns_A_for_game_processes_and_B_otherwise()
    {
        var m = ProcessProfileMap.Default;
        m.SlotFor("cod.exe").Should().Be(AbSlot.A);
        m.SlotFor("ModernWarfare.exe").Should().Be(AbSlot.A);
        m.SlotFor("chrome.exe").Should().Be(AbSlot.B);
        m.SlotFor("Discord.exe").Should().Be(AbSlot.B);
        m.SlotFor("").Should().Be(AbSlot.B);
        m.SlotFor(null).Should().Be(AbSlot.B);
    }

    [Fact]
    public void Custom_process_set_overrides_default()
    {
        var m = new ProcessProfileMap(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cs2.exe", "apex.exe" });
        m.IsGame("cs2.exe").Should().BeTrue();
        m.IsGame("cod.exe").Should().BeFalse();
        m.SlotFor("APEX.EXE").Should().Be(AbSlot.A);
    }
}
