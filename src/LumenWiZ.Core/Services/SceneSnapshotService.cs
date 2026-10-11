using System.Net;
using LumenWiZ.Core.Models;
using LumenWiZ.Core.Network;
using LumenWiZ.Core.Protocol;
using LumenWiZ.Core.Storage;

namespace LumenWiZ.Core.Services;

public class SceneSnapshotService
{
    private readonly WizUdpTransport _transport;
    private readonly ConfigStorage _storage;

    public SceneSnapshotService(WizUdpTransport transport, ConfigStorage storage)
    {
        _transport = transport;
        _storage = storage;
    }

    public static WizCustomScene CreateSceneFromRoomState(WizRoom room, string sceneName, string icon = "💡")
    {
        var scene = new WizCustomScene
        {
            Name = sceneName,
            Icon = icon,
            RoomId = room.RoomId
        };

        foreach (var device in room.Devices)
        {
            scene.DeviceStates[device.Mac] = new WizPilotState
            {
                State = device.CurrentState.State,
                Dimming = device.CurrentState.Dimming,
                R = device.CurrentState.R,
                G = device.CurrentState.G,
                B = device.CurrentState.B,
                Temp = device.CurrentState.Temp,
                SceneId = device.CurrentState.SceneId,
                Speed = device.CurrentState.Speed
            };
        }

        return scene;
    }

    public async Task<WizCustomScene> CaptureLiveRoomSceneAsync(WizRoom room, string sceneName, string icon = "💡")
    {
        // Query live pilot state for every bulb in the room to ensure 100% current state
        foreach (var device in room.Devices)
        {
            if (IPAddress.TryParse(device.IpAddress, out var ip))
            {
                string? response = await _transport.SendAsync(ip, WizProtocol.CreateGetPilotPayload(), 800);
                if (!string.IsNullOrEmpty(response))
                {
                    device.CurrentState = WizProtocol.ParsePilotResponse(response);
                    device.IsOnline = true;
                }
            }
        }

        var scene = CreateSceneFromRoomState(room, sceneName, icon);

        // Persist to storage
        var config = await _storage.LoadConfigAsync();
        config.CustomScenes.RemoveAll(s => s.Name == sceneName && s.RoomId == room.RoomId);
        config.CustomScenes.Add(scene);
        await _storage.SaveConfigAsync(config);

        return scene;
    }

    public async Task ApplySceneAsync(WizCustomScene scene, IEnumerable<WizDevice> roomDevices)
    {
        var deviceLookup = roomDevices.ToDictionary(d => d.Mac);

        foreach (var (mac, pilotState) in scene.DeviceStates)
        {
            if (deviceLookup.TryGetValue(mac, out var device) && IPAddress.TryParse(device.IpAddress, out var ip))
            {
                string payload = WizProtocol.CreateSetPilotPayload(pilotState);
                await _transport.SendAsync(ip, payload, 500);
                device.CurrentState = pilotState;
            }
        }
    }
}
