using System.Net;
using LumenWiZ.Core.Models;
using LumenWiZ.Core.Network;
using LumenWiZ.Core.Protocol;

namespace LumenWiZ.Core.Services;

public class DiscoveryService
{
    private readonly WizUdpTransport _transport;

    public DiscoveryService(WizUdpTransport transport)
    {
        _transport = transport;
    }

    public async Task<List<WizDevice>> DiscoverDevicesAsync(int timeoutMs = 2500)
    {
        string sysConfigMsg = WizProtocol.CreateGetSystemConfigPayload();
        var responses = await _transport.SendBroadcastAsync(sysConfigMsg, timeoutMs);

        var devices = new List<WizDevice>();
        var seenIps = new HashSet<string>();

        foreach (var (ip, json) in responses)
        {
            string ipStr = ip.ToString();
            if (seenIps.Contains(ipStr)) continue;
            seenIps.Add(ipStr);

            var (mac, homeId, roomId) = WizProtocol.ParseSystemConfigResponse(json);
            if (string.IsNullOrEmpty(mac)) continue;

            // Now query its current pilot state
            string? pilotJson = await _transport.SendAsync(ip, WizProtocol.CreateGetPilotPayload(), 1000);
            WizPilotState pilotState = !string.IsNullOrEmpty(pilotJson) 
                ? WizProtocol.ParsePilotResponse(pilotJson) 
                : new WizPilotState();

            devices.Add(new WizDevice
            {
                Mac = mac,
                IpAddress = ipStr,
                Name = $"WiZ Light ({mac[^4..]})",
                HomeId = homeId,
                RoomId = roomId,
                CurrentState = pilotState,
                IsOnline = true
            });
        }

        return devices;
    }

    public static List<WizRoom> GroupDevicesByRoom(IEnumerable<WizDevice> devices, Dictionary<int, string>? customRoomNames = null)
    {
        customRoomNames ??= new Dictionary<int, string>();

        return devices
            .GroupBy(d => d.RoomId)
            .Select(g => new WizRoom
            {
                RoomId = g.Key,
                Name = customRoomNames.TryGetValue(g.Key, out string? name) ? name : $"Habitación #{g.Key}",
                Devices = g.ToList(),
                IsOn = g.Any(d => d.CurrentState.State),
                MasterBrightness = (int)(g.Average(d => d.CurrentState.Dimming ?? 100))
            })
            .OrderBy(r => r.RoomId)
            .ToList();
    }

    public static bool IsLightStripModel(string? moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName)) return false;
        return moduleName.Contains("STRIP", StringComparison.OrdinalIgnoreCase);
    }
}
