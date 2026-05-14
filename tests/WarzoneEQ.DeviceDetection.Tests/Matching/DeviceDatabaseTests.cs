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
}
