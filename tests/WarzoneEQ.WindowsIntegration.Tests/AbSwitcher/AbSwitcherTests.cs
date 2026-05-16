using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Models;
using WarzoneEQ.WindowsIntegration.AbSwitcher;
using WarzoneEQ.WindowsIntegration.EqApo;
using WarzoneEQ.WindowsIntegration.Files;
using Xunit;

namespace WarzoneEQ.WindowsIntegration.Tests.AbSwitcher;

public class AbSwitcherTests : IDisposable
{
    private readonly string _configDir;

    public AbSwitcherTests()
    {
        _configDir = Path.Combine(Path.GetTempPath(), "hearitloud-abswitch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_configDir, "warzone"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_configDir)) Directory.Delete(_configDir, recursive: true);
    }

    private AbSwitcher NewSwitcher() => new(new FakeEqApoLocator(_configDir), new AtomicFileWriter());

    [Fact]
    public void Install_writes_both_slot_files_and_starts_on_slot_A()
    {
        var ab = NewSwitcher();
        ab.Install(
            new ProfileInput(AudioMode.FootstepHunter),
            new ProfileInput(AudioMode.Cinematic));

        File.Exists(ab.PathFor(AbSlot.A)).Should().BeTrue();
        File.Exists(ab.PathFor(AbSlot.B)).Should().BeTrue();
        File.ReadAllText(ab.PathFor(AbSlot.A)).Should().Contain("FootstepHunter");
        File.ReadAllText(ab.PathFor(AbSlot.B)).Should().Contain("Cinematic mode");
        ab.Active.Should().Be(AbSlot.A);
    }

    [Fact]
    public void Selector_file_points_at_active_slot_only()
    {
        var ab = NewSwitcher();
        ab.Install(
            new ProfileInput(AudioMode.FootstepHunter),
            new ProfileInput(AudioMode.Cinematic));
        File.ReadAllText(ab.SelectorPath).Should().Contain("Include: current_A.txt");
        File.ReadAllText(ab.SelectorPath).Should().NotContain("Include: current_B.txt");
    }

    [Fact]
    public void Toggle_alternates_active_slot_and_rewrites_selector()
    {
        var ab = NewSwitcher();
        ab.Install(
            new ProfileInput(AudioMode.FootstepHunter),
            new ProfileInput(AudioMode.Cinematic));

        ab.Toggle().Should().Be(AbSlot.B);
        ab.Active.Should().Be(AbSlot.B);
        File.ReadAllText(ab.SelectorPath).Should().Contain("Include: current_B.txt");

        ab.Toggle().Should().Be(AbSlot.A);
        ab.Active.Should().Be(AbSlot.A);
        File.ReadAllText(ab.SelectorPath).Should().Contain("Include: current_A.txt");
    }

    [Fact]
    public void SwitchTo_is_idempotent()
    {
        var ab = NewSwitcher();
        ab.Install(
            new ProfileInput(AudioMode.FootstepHunter),
            new ProfileInput(AudioMode.Cinematic));
        ab.SwitchTo(AbSlot.B);
        ab.SwitchTo(AbSlot.B);
        ab.Active.Should().Be(AbSlot.B);
    }
}
