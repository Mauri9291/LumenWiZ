using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace LumenWiZ.Core.Network;

public class WizUdpTransport : IDisposable
{
    public const int DefaultPort = 38899;
    private readonly UdpClient _client;
    private bool _disposed;

    public WizUdpTransport(int listenPort = 0)
    {
        _client = new UdpClient(listenPort);
        _client.EnableBroadcast = true;
    }

    public async Task<string?> SendAsync(IPAddress ip, string json, int timeoutMs = 1500)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        var endpoint = new IPEndPoint(ip, DefaultPort);

        await _client.SendAsync(bytes, bytes.Length, endpoint);

        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            var result = await _client.ReceiveAsync(cts.Token);
            return Encoding.UTF8.GetString(result.Buffer);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<List<(IPAddress Ip, string Json)>> SendBroadcastAsync(string json, int timeoutMs = 2500)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        var responses = new List<(IPAddress Ip, string Json)>();

        // Send to global broadcast 255.255.255.255
        try
        {
            await _client.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, DefaultPort));
        }
        catch { }

        // Also send to all local subnet broadcasts
        foreach (var broadcastAddr in GetSubnetBroadcastAddresses())
        {
            try
            {
                await _client.SendAsync(bytes, bytes.Length, new IPEndPoint(broadcastAddr, DefaultPort));
            }
            catch { }
        }

        var endTime = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < endTime)
        {
            int remaining = Math.Max(50, (int)(endTime - DateTime.UtcNow).TotalMilliseconds);
            using var cts = new CancellationTokenSource(remaining);

            try
            {
                var result = await _client.ReceiveAsync(cts.Token);
                string text = Encoding.UTF8.GetString(result.Buffer);
                responses.Add((result.RemoteEndPoint.Address, text));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                break;
            }
        }

        return responses;
    }

    private static IEnumerable<IPAddress> GetSubnetBroadcastAddresses()
    {
        var list = new List<IPAddress>();
        try
        {
            foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (iface.OperationalStatus != OperationalStatus.Up || iface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                foreach (var unicast in iface.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork && unicast.IPv4Mask != null)
                    {
                        byte[] ipBytes = unicast.Address.GetAddressBytes();
                        byte[] maskBytes = unicast.IPv4Mask.GetAddressBytes();
                        byte[] broadcastBytes = new byte[4];
                        for (int i = 0; i < 4; i++)
                        {
                            broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
                        }
                        list.Add(new IPAddress(broadcastBytes));
                    }
                }
            }
        }
        catch { }
        return list;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _client.Dispose();
            _disposed = true;
        }
    }
}
