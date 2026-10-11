using Xunit;
using LumenWiZ.Core.Models;
using LumenWiZ.Core.Services;
using LumenWiZ.Core.Storage;

namespace LumenWiZ.Tests;

public class SceneSnapshotTests
{
    [Fact]
    public void CreateSnapshot_CapturesStatesOfAllDevicesInRoom()
    {
        var devices = new List<WizDevice>
        {
            new() { Mac = "MAC1", RoomId = 10, CurrentState = new WizPilotState { State = true, Dimming = 100, R = 255, G = 50, B = 50 } },
            new() { Mac = "MAC2", RoomId = 10, CurrentState = new WizPilotState { State = true, Dimming = 80, Temp = 2700 } },
            new() { Mac = "MAC3", RoomId = 10, CurrentState = new WizPilotState { State = false, Dimming = 0 } }
        };

        var room = new WizRoom
        {
            RoomId = 10,
            Name = "Living",
            Devices = devices
        };

        WizCustomScene scene = SceneSnapshotService.CreateSceneFromRoomState(room, "Noche de Cine", "🎬");

        Assert.Equal("Noche de Cine", scene.Name);
        Assert.Equal("🎬", scene.Icon);
        Assert.Equal(10, scene.RoomId);
        Assert.Equal(3, scene.DeviceStates.Count);
        Assert.Equal(255, scene.DeviceStates["MAC1"].R);
        Assert.Equal(2700, scene.DeviceStates["MAC2"].Temp);
        Assert.False(scene.DeviceStates["MAC3"].State);
    }

    [Fact]
    public async Task ConfigStorage_SaveAndLoad_RoundTripsCorrectly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "LumenWiZ_Test_" + Guid.NewGuid().ToString("N"));
        var storage = new ConfigStorage(tempDir);

        var config = new AppConfig
        {
            SelectedRoomId = "10",
            RoomNames = new Dictionary<int, string> { [10] = "Mi Cuarto" },
            CustomScenes = new List<WizCustomScene>
            {
                new() { Name = "Chill", Icon = "☕", RoomId = 10 }
            }
        };

        await storage.SaveConfigAsync(config);
        AppConfig loaded = await storage.LoadConfigAsync();

        Assert.Equal("10", loaded.SelectedRoomId);
        Assert.Equal("Mi Cuarto", loaded.RoomNames[10]);
        Assert.Single(loaded.CustomScenes);
        Assert.Equal("Chill", loaded.CustomScenes[0].Name);

        // Cleanup
        try { Directory.Delete(tempDir, true); } catch { }
    }
}
