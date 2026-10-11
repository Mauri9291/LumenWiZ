using Xunit;
using LumenWiZ.Core.Models;
using LumenWiZ.Core.Protocol;

namespace LumenWiZ.Tests;

public class ProtocolTests
{
    [Fact]
    public void ParsePilotResponse_ValidJson_ReturnsPopulatedState()
    {
        string sampleJson = "{\"method\":\"getPilot\",\"env\":\"pro\",\"result\":{\"mac\":\"a8bb50\",\"rssi\":-60,\"state\":true,\"sceneId\":0,\"speed\":100,\"dimming\":80,\"temp\":2700,\"r\":255,\"g\":120,\"b\":40}}";

        WizPilotState state = WizProtocol.ParsePilotResponse(sampleJson);

        Assert.NotNull(state);
        Assert.True(state.State);
        Assert.Equal(80, state.Dimming);
        Assert.Equal(2700, state.Temp);
        Assert.Equal(255, state.R);
        Assert.Equal(120, state.G);
        Assert.Equal(40, state.B);
    }

    [Fact]
    public void CreateSetPilotPayload_ReturnsValidJsonString()
    {
        var state = new WizPilotState
        {
            State = true,
            Dimming = 90,
            R = 255,
            G = 0,
            B = 128
        };

        string json = WizProtocol.CreateSetPilotPayload(state);

        Assert.Contains("\"method\":\"setPilot\"", json);
        Assert.Contains("\"state\":true", json);
        Assert.Contains("\"dimming\":90", json);
        Assert.Contains("\"r\":255", json);
        Assert.Contains("\"b\":128", json);
    }

    [Fact]
    public void ParseSystemConfigResponse_ReturnsMacHomeAndRoom()
    {
        string sampleConfig = "{\"method\":\"getSystemConfig\",\"env\":\"pro\",\"result\":{\"mac\":\"a8bb50607080\",\"homeId\":98765,\"roomId\":4321,\"rgn\":\"eu\",\"moduleName\":\"ESP05_SHRGB_21\"}}";

        var (mac, homeId, roomId) = WizProtocol.ParseSystemConfigResponse(sampleConfig);

        Assert.Equal("a8bb50607080", mac);
        Assert.Equal(98765, homeId);
        Assert.Equal(4321, roomId);
    }
}
