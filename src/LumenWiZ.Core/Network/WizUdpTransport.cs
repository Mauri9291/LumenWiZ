using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace LumenWiZ.Core.Network;

public class WizUdpTransport : IDisposable
{
    public const int DefaultPort = 38899;
    private readonly UdpClient _client;
    private readonly SemaphoreSlim _socketLock = new(1, 1);
    private bool _disposed;

    public WizUdpTransport(int listenPort = 0)
    {
        _client = new UdpClient(listenPort);
        _client.EnableBroadcast = true;

        // Fix Winsock WSAECONNRESET (error 10054) on Windows when ICMP Port Unreachable arrives
        try
        {
            const int SIO_UDP_CONNRESET = -1744830452; // (int)0x9800000C
            _client.Client.IOControl(SIO_UDP_CONNRESET, new byte[] { 0 }, new byte[] { 0 });
        }
        catch { }
    }

    public void SendFireAndForget(IPAddress ip, string json)
    {
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            _client.SendAsync(bytes, bytes.Length, new IPEndPoint(ip, DefaultPort));
        }
        catch { }
    }

    public async Task<string?> SendAsync(IPAddress ip, string json, int timeoutMs = 1500)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        var endpoint = new IPEndPoint(ip, DefaultPort);

        await _socketLock.WaitAsync();
        try
        {
            try
            {
                await _client.SendAsync(bytes, bytes.Length, endpoint);
            }
            catch
            {
                return null;
            }

            var endTime = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < endTime)
            {
                int remaining = Math.Max(50, (int)(endTime - DateTime.UtcNow).TotalMilliseconds);
                using var cts = new CancellationTokenSource(remaining);

                try
                {
                    var result = await _client.ReceiveAsync(cts.Token);
                    if (result.RemoteEndPoint.Address.Equals(ip))
                    {
                        return Encoding.UTF8.GetString(result.Buffer);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SocketException)
                {
                    continue;
                }
                catch
                {
                    break;
                }
            }

            return null;
        }
        finally
        {
            _socketLock.Release();
        }
    }

    public async Task<List<(IPAddress Ip, string Json)>> SendBroadcastAsync(string json, int timeoutMs = 2500)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        var responses = new List<(IPAddress Ip, string Json)>();

        await _socketLock.WaitAsync();
        try
        {
            // 1. Global broadcast
            try
            {
                await _client.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, DefaultPort));
            }
            catch { }

            // 2. Subnet broadcasts
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
                catch (SocketException)
                {
                    continue;
                }
                catch
                {
                    break;
                }
            }

            return responses;
        }
        finally
        {
            _socketLock.Release();
        }
    }

    public async Task<List<(IPAddress Ip, string Json)>> ScanSubnetAsync(string json, int timeoutMs = 2000)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        var responses = new List<(IPAddress Ip, string Json)>();
        var seenIps = new HashSet<IPAddress>();

        // Collect all /24 IPs for each local network interface
        var targetIps = new List<IPAddress>();
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

                        // Match /24 subnet (255.255.255.0)
                        if (maskBytes[0] == 255 && maskBytes[1] == 255 && maskBytes[2] == 255)
                        {
                            for (int i = 1; i <= 254; i++)
                            {
                                if (i != ipBytes[3])
                                {
                                    targetIps.Add(new IPAddress(new byte[] { ipBytes[0], ipBytes[1], ipBytes[2], (byte)i }));
                                }
                            }
                        }
                    }
                }
            }
        }
        catch { }

        await _socketLock.WaitAsync();
        try
        {
            // Send unicast query to all candidate IPs
            foreach (var ip in targetIps)
            {
                try
                {
                    await _client.SendAsync(bytes, bytes.Length, new IPEndPoint(ip, DefaultPort));
                }
                catch { }
            }

            // Collect responses until timeout
            var endTime = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < endTime)
            {
                int remaining = Math.Max(50, (int)(endTime - DateTime.UtcNow).TotalMilliseconds);
                using var cts = new CancellationTokenSource(remaining);

                try
                {
                    var result = await _client.ReceiveAsync(cts.Token);
                    if (seenIps.Add(result.RemoteEndPoint.Address))
                    {
                        string text = Encoding.UTF8.GetString(result.Buffer);
                        responses.Add((result.RemoteEndPoint.Address, text));
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SocketException)
                {
                    continue;
                }
                catch
                {
                    break;
                }
            }

            return responses;
        }
        finally
        {
            _socketLock.Release();
        }
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
            _socketLock.Dispose();
            _disposed = true;
        }
    }
}
