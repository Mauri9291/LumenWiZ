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

    public async Task<List<WizDevice>> DiscoverDevicesAsync(
        int timeoutMs = 1800, 
        Dictionary<string, string>? customDeviceNames = null,
        Dictionary<string, string>? customAudioBands = null)
    {
        string sysConfigMsg = WizProtocol.CreateGetSystemConfigPayload();
        
        // Fast reliable unicast sweep across /24 subnet (router never drops unicast between Ethernet & Wi-Fi)
        var responses = await _transport.ScanSubnetAsync(sysConfigMsg, timeoutMs);

        // Fallback to broadcast only if subnet sweep found nothing
        if (responses.Count == 0)
        {
            responses = await _transport.SendBroadcastAsync(sysConfigMsg, 1200);
        }

        var devices = new List<WizDevice>();
        var seenIps = new HashSet<string>();
        var validConfigs = new List<(IPAddress Ip, string IpStr, string Mac, int HomeId, int RoomId, bool IsStrip)>();

        foreach (var (ip, json) in responses)
        {
            string ipStr = ip.ToString();
            if (seenIps.Contains(ipStr)) continue;
            seenIps.Add(ipStr);

            var (mac, homeId, roomId, moduleName) = WizProtocol.ParseSystemConfigResponseFull(json);
            if (string.IsNullOrEmpty(mac)) continue;

            bool isStrip = IsLightStripModel(moduleName);
            validConfigs.Add((ip, ipStr, mac, homeId, roomId, isStrip));
        }

        // Query current pilot state for each discovered device
        foreach (var c in validConfigs)
        {
            string? pilotJson = await _transport.SendAsync(c.Ip, WizProtocol.CreateGetPilotPayload(), 400);
            WizPilotState pilotState = !string.IsNullOrEmpty(pilotJson) 
                ? WizProtocol.ParsePilotResponse(pilotJson) 
                : new WizPilotState();

            string suffix = c.Mac.Length >= 4 ? c.Mac[^4..] : c.Mac;
            string defaultName = c.IsStrip ? "Tira LED Escritorio" : $"Foco {suffix}";
            string name = (customDeviceNames != null && customDeviceNames.TryGetValue(c.Mac, out var cName))
                ? cName
                : defaultName;

            string defaultBand = c.IsStrip ? "Bass" : "Mids";
            string band = (customAudioBands != null && customAudioBands.TryGetValue(c.Mac, out var cBand))
                ? cBand
                : defaultBand;

            devices.Add(new WizDevice
            {
                Mac = c.Mac,
                IpAddress = c.IpStr,
                Name = name,
                HomeId = c.HomeId,
                RoomId = c.RoomId,
                IsLightStrip = c.IsStrip,
                AudioBand = band,
                CurrentState = pilotState,
                IsOnline = true
            });
        }

        return devices;
    }

    public static List<WizRoom> GroupDevicesByRoom(
        IEnumerable<WizDevice> devices, 
        Dictionary<string, string>? deviceRoomAssignments = null,
        List<string>? customRooms = null,
        Dictionary<int, string>? customRoomNames = null)
    {
        var devList = devices.ToList();
        var result = new List<WizRoom>();

        // 1. "General" space with all bulbs
        if (devList.Count > 0)
        {
            result.Add(new WizRoom
            {
                RoomId = -1,
                Name = "General",
                Devices = devList.ToList(),
                IsOn = devList.Any(d => d.CurrentState.State),
                MasterBrightness = (int)devList.Average(d => d.CurrentState.Dimming ?? 80)
            });
        }

        deviceRoomAssignments ??= new Dictionary<string, string>();
        var roomNamesList = customRooms != null && customRooms.Count > 0 
            ? customRooms.ToList() 
            : new List<string> { "Mi Habitación" };

        var roomMap = new Dictionary<string, List<WizDevice>>(StringComparer.OrdinalIgnoreCase);
        foreach (var rName in roomNamesList)
        {
            if (!string.IsNullOrWhiteSpace(rName) && !roomMap.ContainsKey(rName))
            {
                roomMap[rName] = new List<WizDevice>();
            }
        }

        string fallbackRoom = roomNamesList[0];
        foreach (var d in devList)
        {
            string assigned = deviceRoomAssignments.TryGetValue(d.Mac, out var r) && !string.IsNullOrWhiteSpace(r)
                ? r
                : fallbackRoom;

            if (!roomMap.ContainsKey(assigned))
            {
                roomMap[assigned] = new List<WizDevice>();
            }
            roomMap[assigned].Add(d);
        }

        int rId = 1;
        foreach (var kvp in roomMap)
        {
            var rDevs = kvp.Value;
            result.Add(new WizRoom
            {
                RoomId = rId++,
                Name = kvp.Key,
                Devices = rDevs,
                IsOn = rDevs.Any(d => d.CurrentState.State),
                MasterBrightness = rDevs.Count > 0 ? (int)rDevs.Average(d => d.CurrentState.Dimming ?? 80) : 80
            });
        }

        return result;
    }

    public static bool IsLightStripModel(string? moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName)) return false;
        return moduleName.Contains("STRIP", StringComparison.OrdinalIgnoreCase)
            || moduleName.Contains("ESP20", StringComparison.OrdinalIgnoreCase);
    }
}
