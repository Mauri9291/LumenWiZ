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
        
        // Fast dual discovery: parallel subnet unicast sweep + UDP broadcast
        var subnetTask = _transport.ScanSubnetAsync(sysConfigMsg, timeoutMs);
        var broadcastTask = _transport.SendBroadcastAsync(sysConfigMsg, timeoutMs);

        await Task.WhenAll(subnetTask, broadcastTask);

        var responses = new List<(IPAddress Ip, string Json)>();
        responses.AddRange(subnetTask.Result);
        responses.AddRange(broadcastTask.Result);

        var devices = new List<WizDevice>();
        var seenIps = new HashSet<string>();

        foreach (var (ip, json) in responses)
        {
            string ipStr = ip.ToString();
            if (seenIps.Contains(ipStr)) continue;
            seenIps.Add(ipStr);

            var (mac, homeId, roomId, moduleName) = WizProtocol.ParseSystemConfigResponseFull(json);
            if (string.IsNullOrEmpty(mac)) continue;

            bool isStrip = IsLightStripModel(moduleName);

            // Query current pilot state for this device
            string? pilotJson = await _transport.SendAsync(ip, WizProtocol.CreateGetPilotPayload(), 800);
            WizPilotState pilotState = !string.IsNullOrEmpty(pilotJson) 
                ? WizProtocol.ParsePilotResponse(pilotJson) 
                : new WizPilotState();

            devices.Add(new WizDevice
            {
                Mac = mac,
                IpAddress = ipStr,
                Name = isStrip ? "Tira LED Escritorio" : $"Foco {mac[^4..]}",
                HomeId = homeId,
                RoomId = roomId,
                IsLightStrip = isStrip,
                CurrentState = pilotState,
                IsOnline = true
            });
        }

        return devices;
    }

    public static List<WizRoom> GroupDevicesByRoom(IEnumerable<WizDevice> devices, Dictionary<int, string>? customRoomNames = null)
    {
        customRoomNames ??= new Dictionary<int, string>();

        var devList = devices.ToList();
        var groups = devList.GroupBy(d => d.RoomId).ToList();

        return groups
            .Select(g => new WizRoom
            {
                RoomId = g.Key,
                Name = customRoomNames.TryGetValue(g.Key, out string? name) 
                    ? name 
                    : (groups.Count == 1 ? "Mi Habitación" : $"Habitación #{g.Key}"),
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
        return moduleName.Contains("STRIP", StringComparison.OrdinalIgnoreCase)
            || moduleName.Contains("ESP20", StringComparison.OrdinalIgnoreCase);
    }
}
