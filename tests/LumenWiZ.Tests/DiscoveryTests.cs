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

        var assignments = new Dictionary<string, string>
        {
            ["MAC1"] = "Mi Habitación",
            ["MAC2"] = "Mi Habitación",
            ["MAC3"] = "Mi Habitación",
            ["MAC4"] = "Cocina"
        };
        var customRooms = new List<string> { "Mi Habitación", "Cocina" };

        List<WizRoom> rooms = DiscoveryService.GroupDevicesByRoom(devices, assignments, customRooms);

        Assert.Equal(3, rooms.Count);

        var general = rooms.FirstOrDefault(r => r.RoomId == -1);
        Assert.NotNull(general);
        Assert.Equal("General", general.Name);
        Assert.Equal(4, general.Devices.Count);

        var myRoom = rooms.FirstOrDefault(r => r.Name == "Mi Habitación");
        Assert.NotNull(myRoom);
        Assert.Equal(3, myRoom.Devices.Count);

        var kitchen = rooms.FirstOrDefault(r => r.Name == "Cocina");
        Assert.NotNull(kitchen);
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
