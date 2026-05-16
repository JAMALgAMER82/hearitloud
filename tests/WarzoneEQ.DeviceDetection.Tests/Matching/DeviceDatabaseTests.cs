using FluentAssertions;
using WarzoneEQ.DeviceDetection.Matching;
using Xunit;

namespace WarzoneEQ.DeviceDetection.Tests.Matching;

public class DeviceDatabaseTests
{
    [Fact]
    public void Loads_default_embedded_database()
    {
        var db = DeviceDatabase.LoadEmbedded();
        db.HeadphoneCount.Should().BeGreaterThan(0);
        db.MultiEndpointDacCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Lookup_known_headphone_VID_PID_returns_match()
    {
        var db = DeviceDatabase.LoadEmbedded();
        var match = db.LookupHeadphoneByVidPid("VID_1532&PID_0517");
        match.Should().NotBeNull();
        match!.Model.Should().Be("Razer BlackShark V2 Pro");
        match.AutoeqSlug.Should().Be("razer/BlackShark_V2_Pro");
    }

    [Fact]
    public void Lookup_known_multi_endpoint_DAC_VID_PID_returns_match()
    {
        var db = DeviceDatabase.LoadEmbedded();
        var dac = db.LookupDacByVidPid("VID_041E&PID_3260");
        dac.Should().NotBeNull();
        dac!.Model.Should().Be("Creative Sound Blaster GC7");
        dac.GameEndpoint.Should().Contain("Game");
        dac.VoiceEndpoint.Should().Contain("Chat");
    }

    [Fact]
    public void Lookup_unknown_VID_PID_returns_null()
    {
        var db = DeviceDatabase.LoadEmbedded();
        db.LookupHeadphoneByVidPid("VID_FFFF&PID_FFFF").Should().BeNull();
        db.LookupDacByVidPid("VID_FFFF&PID_FFFF").Should().BeNull();
    }

    [Fact]
    public void Lookup_known_bluetooth_name_returns_autoeq_slug()
    {
        var db = DeviceDatabase.LoadEmbedded();
        db.LookupHeadphoneByBluetoothName("WH-1000XM5").Should().NotBeNull();
        db.LookupHeadphoneByBluetoothName("WH-1000XM5")!.AutoeqSlug.Should().Be("sony/WH-1000XM5");
    }

    // The v1.7 substring matcher catches the variants Windows actually
    // reports — bare, manufacturer-prefixed, and connection-mode-suffixed.
    [Theory]
    [InlineData("WH-1000XM5",                                 "sony/WH-1000XM5")]
    [InlineData("Sony WH-1000XM5",                            "sony/WH-1000XM5")]
    [InlineData("WH-1000XM5 - Hands-Free AG",                 "sony/WH-1000XM5")]
    [InlineData("WH-1000XM4 - Stereo",                        "sony/WH-1000XM4")]
    [InlineData("Headphones (AirPods Pro 2 - Hands-Free AG)", "apple/AirPods_Pro_2")]
    [InlineData("Headphones (Cloud III Wireless Stereo)",     "hyperx/Cloud_III_Wireless")]
    public void Bluetooth_lookup_is_substring_aware(string deviceName, string expectedSlug)
    {
        var db = DeviceDatabase.LoadEmbedded();
        var match = db.LookupHeadphoneByBluetoothName(deviceName);
        match.Should().NotBeNull(because: $"\"{deviceName}\" contains a registered BT pattern");
        match!.AutoeqSlug.Should().Be(expectedSlug);
    }

    [Fact]
    public void Bluetooth_lookup_returns_longest_matching_pattern()
    {
        // "Cloud III Wireless" should beat the shorter "Cloud III" when both
        // appear in the device name. (Longest-prefix wins prevents the
        // wireless model from being misidentified as the wired one.)
        var db = DeviceDatabase.LoadEmbedded();
        db.LookupHeadphoneByBluetoothName("My Cloud III Wireless Stereo")!
            .AutoeqSlug.Should().Be("hyperx/Cloud_III_Wireless");
    }

    [Fact]
    public void Bluetooth_lookup_returns_null_for_unrelated_device()
    {
        var db = DeviceDatabase.LoadEmbedded();
        db.LookupHeadphoneByBluetoothName("My Random Speaker XYZ").Should().BeNull();
    }

    [Fact]
    public void Comment_keys_in_JSON_overlay_are_skipped_not_treated_as_entries()
    {
        // The expanded overlay uses "_comment" and "//section" string-value
        // keys for human-readable group dividers; the loader must ignore them.
        var db = DeviceDatabase.LoadEmbedded();
        db.LookupHeadphoneByVidPid("_comment").Should().BeNull();
        db.LookupHeadphoneByVidPid("//razer").Should().BeNull();
        db.LookupHeadphoneByBluetoothName("//sony").Should().BeNull();
    }

    [Fact]
    public void Expanded_overlay_covers_at_least_50_headphones_and_10_DACs()
    {
        // Sanity check that future trimming of the overlay can't silently
        // shrink the hardware coverage below v1.7's baseline.
        var db = DeviceDatabase.LoadEmbedded();
        db.HeadphoneCount.Should().BeGreaterThanOrEqualTo(50);
        db.MultiEndpointDacCount.Should().BeGreaterThanOrEqualTo(10);
    }
}
