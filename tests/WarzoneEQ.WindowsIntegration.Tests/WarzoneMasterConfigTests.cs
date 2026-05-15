using FluentAssertions;
using WarzoneEQ.WindowsIntegration;
using Xunit;

namespace WarzoneEQ.WindowsIntegration.Tests;

public class WarzoneMasterConfigTests
{
    [Fact]
    public void BuildBlock_uses_app_predicate_for_every_warzone_process()
    {
        var block = WarzoneMasterConfig.BuildBlock();
        foreach (var proc in WarzoneMasterConfig.WarzoneProcesses)
            block.Should().Contain($"app:{proc}");
        block.Should().StartWith(WarzoneMasterConfig.BlockStartMarker);
        block.Should().EndWith(WarzoneMasterConfig.BlockEndMarker);
        block.Should().Contain("If(");
        block.Should().Contain("EndIf");
        block.Should().Contain(WarzoneMasterConfig.IncludeLine);
    }

    [Fact]
    public void Merge_into_empty_config_writes_the_block()
    {
        var merged = WarzoneMasterConfig.Merge("");
        merged.Should().Contain(WarzoneMasterConfig.BlockStartMarker);
        merged.Should().Contain("If(app:cod.exe");
    }

    [Fact]
    public void Merge_appends_block_when_master_has_other_directives()
    {
        var input = "Preamp: 0 dB\nFilter: ON HP Fc 80 Hz\n";
        var merged = WarzoneMasterConfig.Merge(input);
        merged.Should().StartWith(input);
        merged.Should().Contain(WarzoneMasterConfig.BlockStartMarker);
    }

    [Fact]
    public void Merge_upgrades_legacy_bare_include_to_conditional_block()
    {
        var legacy = "Preamp: 0 dB\nInclude: warzone\\current.txt\nFilter: ON HP Fc 80 Hz\n";
        var merged = WarzoneMasterConfig.Merge(legacy);
        merged.Should().Contain(WarzoneMasterConfig.BlockStartMarker);
        merged.Should().Contain("Preamp: 0 dB");
        merged.Should().Contain("Filter: ON HP Fc 80 Hz");
        // The Include line is now wrapped inside the If block, not bare in the master config.
        System.Text.RegularExpressions.Regex.Matches(merged, @"Include: warzone\\current\.txt").Count
            .Should().Be(1);
    }

    [Fact]
    public void Merge_replaces_existing_managed_block_in_place()
    {
        var first = WarzoneMasterConfig.Merge("Preamp: 0 dB\n");
        var second = WarzoneMasterConfig.Merge(first);
        second.Should().Be(first, because: "merge must be idempotent when the managed block is already present");
        System.Text.RegularExpressions.Regex.Matches(second, WarzoneMasterConfig.BlockStartMarker).Count
            .Should().Be(1);
    }

    [Fact]
    public void Merge_preserves_content_outside_the_managed_block()
    {
        var before = "Preamp: -2 dB\nFilter: ON LP Fc 16000 Hz\n";
        var after = "Filter: ON PK Fc 4000 Hz Gain +3 dB\n";
        var input = before + WarzoneMasterConfig.BuildBlock() + "\n" + after;
        var merged = WarzoneMasterConfig.Merge(input);
        merged.Should().StartWith(before);
        merged.Should().EndWith(after);
    }

    [Fact]
    public void HasManagedBlock_detects_markers()
    {
        WarzoneMasterConfig.HasManagedBlock(WarzoneMasterConfig.BuildBlock()).Should().BeTrue();
        WarzoneMasterConfig.HasManagedBlock("").Should().BeFalse();
        WarzoneMasterConfig.HasManagedBlock("Include: warzone\\current.txt").Should().BeFalse();
    }

    [Fact]
    public void RemoveManagedBlock_strips_block_and_preserves_surrounding_content()
    {
        var before = "Preamp: -2 dB\nFilter: ON LP Fc 16000 Hz\n";
        var after = "Filter: ON PK Fc 4000 Hz Gain +3 dB\n";
        var input = before + WarzoneMasterConfig.BuildBlock() + Environment.NewLine + after;
        var cleaned = WarzoneMasterConfig.RemoveManagedBlock(input);
        cleaned.Should().NotContain(WarzoneMasterConfig.BlockStartMarker);
        cleaned.Should().NotContain(WarzoneMasterConfig.BlockEndMarker);
        cleaned.Should().Contain("Preamp: -2 dB");
        cleaned.Should().Contain("Filter: ON PK Fc 4000 Hz Gain +3 dB");
    }

    [Fact]
    public void RemoveManagedBlock_also_strips_legacy_bare_include_lines()
    {
        var input = "Preamp: 0 dB\nInclude: warzone\\current.txt\nFilter: ON HP Fc 80 Hz\n";
        var cleaned = WarzoneMasterConfig.RemoveManagedBlock(input);
        cleaned.Should().NotContain("warzone\\current.txt");
        cleaned.Should().Contain("Preamp: 0 dB");
        cleaned.Should().Contain("Filter: ON HP Fc 80 Hz");
    }

    [Fact]
    public void RemoveManagedBlock_on_clean_config_is_noop()
    {
        var input = "Preamp: 0 dB\nFilter: ON HP Fc 80 Hz\n";
        WarzoneMasterConfig.RemoveManagedBlock(input).Should().Be(input);
    }

    [Fact]
    public void HasLegacyBareInclude_distinguishes_bare_from_managed()
    {
        WarzoneMasterConfig.HasLegacyBareInclude("Include: warzone\\current.txt\n").Should().BeTrue();
        WarzoneMasterConfig.HasLegacyBareInclude(WarzoneMasterConfig.BuildBlock()).Should().BeFalse();
        WarzoneMasterConfig.HasLegacyBareInclude("").Should().BeFalse();
    }
}
