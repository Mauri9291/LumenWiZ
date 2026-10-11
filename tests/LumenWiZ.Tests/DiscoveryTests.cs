using Xunit;
using LumenWiZ.Core.Models;
using LumenWiZ.Core.Services;

namespace LumenWiZ.Tests;

public class DiscoveryTests
{
    [Fact]
    public void GroupDevicesByRoom_CorrectlyAggregatesDevices()
    {
        var devices = new List<WizDevice>
        {
            new() { Mac = "MAC1", RoomId = 101, Name = "Bulb 1" },
            new() { Mac = "MAC2", RoomId = 101, Name = "Bulb 2" },
            new() { Mac = "MAC3", RoomId = 101, Name = "LED Strip", IsLightStrip = true },
            new() { Mac = "MAC4", RoomId = 202, Name = "Kitchen Light" }
        };

        var customNames = new Dictionary<int, string>
        {
            [101] = "Mi Habitación",
            [202] = "Cocina"
        };

        List<WizRoom> rooms = DiscoveryService.GroupDevicesByRoom(devices, customNames);

        Assert.Equal(2, rooms.Count);

        var myRoom = rooms.FirstOrDefault(r => r.RoomId == 101);
        Assert.NotNull(myRoom);
        Assert.Equal("Mi Habitación", myRoom.Name);
        Assert.Equal(3, myRoom.Devices.Count);

        var kitchen = rooms.FirstOrDefault(r => r.RoomId == 202);
        Assert.NotNull(kitchen);
        Assert.Equal("Cocina", kitchen.Name);
        Assert.Single(kitchen.Devices);
    }

    [Fact]
    public void DetermineIfLightStrip_IdentifiesStripByModuleName()
    {
        bool isStrip1 = DiscoveryService.IsLightStripModel("ESP05_SHRGB_21");
        bool isStrip2 = DiscoveryService.IsLightStripModel("ESP01_SHSTRIP_01");
        bool isStrip3 = DiscoveryService.IsLightStripModel("ESP01_SHDW_01");

        Assert.True(isStrip2);
        Assert.False(isStrip3);
    }
}
