using System.Net;
using LumenWiZ.Core.Network;
using LumenWiZ.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace LumenWiZ.Tests;

public class LiveDiscoveryTests
{
    private readonly ITestOutputHelper _output;

    public LiveDiscoveryTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task LiveNetwork_DiscoversWizDevices()
    {
        using var transport = new WizUdpTransport(0);
        var discovery = new DiscoveryService(transport);

        var devices = await discovery.DiscoverDevicesAsync(2500);

        _output.WriteLine($"Discovered {devices.Count} devices:");
        foreach (var dev in devices)
        {
            _output.WriteLine($" - {dev.Name} (IP: {dev.IpAddress}, MAC: {dev.Mac}, Strip: {dev.IsLightStrip}, Room: {dev.RoomId})");
        }

        Assert.NotEmpty(devices);
    }
}
