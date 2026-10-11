using System.Text.Json.Serialization;

namespace LumenWiZ.Core.Models;

public class WizPilotState
{
    [JsonPropertyName("state")]
    public bool State { get; set; } = true;

    [JsonPropertyName("dimming")]
    public int? Dimming { get; set; }

    [JsonPropertyName("r")]
    public int? R { get; set; }

    [JsonPropertyName("g")]
    public int? G { get; set; }

    [JsonPropertyName("b")]
    public int? B { get; set; }

    [JsonPropertyName("temp")]
    public int? Temp { get; set; }

    [JsonPropertyName("sceneId")]
    public int? SceneId { get; set; }

    [JsonPropertyName("speed")]
    public int? Speed { get; set; }
}

public class WizDevice
{
    public string Mac { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int HomeId { get; set; }
    public int RoomId { get; set; }
    public bool IsLightStrip { get; set; }
    public WizPilotState CurrentState { get; set; } = new();
    public bool AudioSyncEnabled { get; set; } = true;
    public bool IsOnline { get; set; } = true;
}

public class WizRoom
{
    public int RoomId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<WizDevice> Devices { get; set; } = new();
    public int MasterBrightness { get; set; } = 100;
    public bool IsOn { get; set; } = true;
}

public class WizCustomScene
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "💡";
    public int RoomId { get; set; }
    public Dictionary<string, WizPilotState> DeviceStates { get; set; } = new();
}

public class AppConfig
{
    public string SelectedRoomId { get; set; } = string.Empty;
    public Dictionary<int, string> RoomNames { get; set; } = new();
    public Dictionary<string, string> DeviceNames { get; set; } = new();
    public List<WizCustomScene> CustomScenes { get; set; } = new();
    public int HttpPort { get; set; } = 38898;
}
