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
    public string AudioBand { get; set; } = "Bass"; // "Bass", "Mids", "Treble", "Full"
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
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "💡";
    public int RoomId { get; set; }
    public bool ShowInQuickScenes { get; set; } = true;
    public Dictionary<string, WizPilotState> DeviceStates { get; set; } = new();
}

public class QuickSceneItemConfig
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "💡";
    public int? SceneId { get; set; } // WiZ hardware scene ID (e.g. 12 = Luz Diaria, 5 = Chimenea)
    public string? CustomSceneName { get; set; } // Custom saved scene name if user-defined
    public bool IsAudioSync { get; set; }
}

public class AppConfig
{
    public string SelectedRoomId { get; set; } = string.Empty;
    public Dictionary<int, string> RoomNames { get; set; } = new();
    public Dictionary<string, string> DeviceNames { get; set; } = new();
    public Dictionary<string, string> DeviceAudioBands { get; set; } = new();
    public List<string> DeviceOrder { get; set; } = new();
    public Dictionary<string, string> DeviceRoomAssignments { get; set; } = new();
    public List<string> CustomRooms { get; set; } = new();
    public List<WizCustomScene> CustomScenes { get; set; } = new();
    public List<QuickSceneItemConfig> QuickScenes { get; set; } = new();
    public int HttpPort { get; set; } = 38898;
    public string Language { get; set; } = "es";
    public bool StartWithWindows { get; set; } = true;
    public bool StartMinimized { get; set; } = true;
}
