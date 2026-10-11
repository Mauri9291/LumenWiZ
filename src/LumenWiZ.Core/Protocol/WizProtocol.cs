using System.Text.Json;
using System.Text.Json.Nodes;
using LumenWiZ.Core.Models;

namespace LumenWiZ.Core.Protocol;

public static class WizProtocol
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static string CreateSetPilotPayload(WizPilotState state)
    {
        var root = new JsonObject
        {
            ["method"] = "setPilot",
            ["params"] = JsonSerializer.SerializeToNode(state, JsonOptions)
        };
        return root.ToJsonString();
    }

    public static string CreateGetPilotPayload()
    {
        return "{\"method\":\"getPilot\",\"params\":{}}";
    }

    public static string CreateGetSystemConfigPayload()
    {
        return "{\"method\":\"getSystemConfig\",\"params\":{}}";
    }

    public static WizPilotState ParsePilotResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("result", out var result))
        {
            return JsonSerializer.Deserialize<WizPilotState>(result.GetRawText(), JsonOptions) ?? new WizPilotState();
        }

        return new WizPilotState();
    }

    public static (string mac, int homeId, int roomId) ParseSystemConfigResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string mac = string.Empty;
        int homeId = 0;
        int roomId = 0;

        if (root.TryGetProperty("result", out var result))
        {
            if (result.TryGetProperty("mac", out var macProp))
                mac = macProp.GetString() ?? string.Empty;

            if (result.TryGetProperty("homeId", out var homeProp) && homeProp.TryGetInt32(out var h))
                homeId = h;

            if (result.TryGetProperty("roomId", out var roomProp) && roomProp.TryGetInt32(out var r))
                roomId = r;
        }

        return (mac, homeId, roomId);
    }
}
