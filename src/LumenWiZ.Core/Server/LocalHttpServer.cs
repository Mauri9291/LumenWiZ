using System.Net;
using System.Text;
using System.Text.Json;
using LumenWiZ.Core.Models;

namespace LumenWiZ.Core.Server;

public class LocalHttpServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly int _port;
    private bool _isRunning;
    private CancellationTokenSource? _cts;

    public Func<IEnumerable<WizRoom>>? GetRoomsHandler { get; set; }
    public Func<int, bool, Task>? ToggleRoomPowerHandler { get; set; }
    public Func<string, Task>? ActivateSceneHandler { get; set; }
    public Func<int, int, Task>? SetRoomBrightnessHandler { get; set; }

    public int Port => _port;
    public bool IsRunning => _isRunning;

    public LocalHttpServer(int port = 38898)
    {
        _port = port;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
    }

    public void Start()
    {
        if (_isRunning) return;

        try
        {
            _listener.Start();
            _isRunning = true;
            _cts = new CancellationTokenSource();
            Task.Run(ListenLoopAsync);
        }
        catch (Exception ex)
        {
            string logFile = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LumenWiZ", "debug.log");
            System.IO.File.AppendAllText(logFile, $"[LocalHttpServer ERROR] {ex}\n");
            _isRunning = false;
        }
    }

    public void Stop()
    {
        if (!_isRunning) return;

        _isRunning = false;
        _cts?.Cancel();
        try { _listener.Stop(); } catch { }
    }

    private async Task ListenLoopAsync()
    {
        while (_isRunning && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = ProcessRequestAsync(context);
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (Exception)
            {
                if (!_isRunning) break;
            }
        }
    }

    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        var req = context.Request;
        var res = context.Response;

        // CORS and security headers for local Stream Deck calls
        res.Headers.Add("Access-Control-Allow-Origin", "*");
        res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        res.Headers.Add("Content-Type", "application/json; charset=utf-8");

        if (req.HttpMethod == "OPTIONS")
        {
            res.StatusCode = 200;
            res.Close();
            return;
        }

        string path = req.Url?.AbsolutePath.ToLowerInvariant() ?? "";
        string responseJson = "{\"status\":\"error\",\"message\":\"Not found\"}";
        int statusCode = 404;

        try
        {
            if (path == "/api/status" || path == "/api/status/")
            {
                var rooms = GetRoomsHandler?.Invoke() ?? Enumerable.Empty<WizRoom>();
                responseJson = JsonSerializer.Serialize(new
                {
                    status = "ok",
                    version = "1.0.0",
                    rooms = rooms.Select(r => new { r.RoomId, r.Name, r.IsOn, r.MasterBrightness, DeviceCount = r.Devices.Count })
                });
                statusCode = 200;
            }
            else if (path.StartsWith("/api/power/toggle"))
            {
                int roomId = int.TryParse(req.QueryString["room"], out int rId) ? rId : 0;
                bool state = bool.TryParse(req.QueryString["state"], out bool s) ? s : true;
                if (ToggleRoomPowerHandler != null)
                {
                    await ToggleRoomPowerHandler.Invoke(roomId, state);
                }
                responseJson = "{\"status\":\"ok\",\"action\":\"toggled\"}";
                statusCode = 200;
            }
            else if (path.StartsWith("/api/scenes/activate"))
            {
                string? sceneName = req.QueryString["name"];
                if (!string.IsNullOrEmpty(sceneName) && ActivateSceneHandler != null)
                {
                    await ActivateSceneHandler.Invoke(sceneName);
                    responseJson = $"{{\"status\":\"ok\",\"scene\":\"{sceneName}\"}}";
                    statusCode = 200;
                }
            }
            else if (path.StartsWith("/api/brightness"))
            {
                int roomId = int.TryParse(req.QueryString["room"], out int rId) ? rId : 0;
                int brightness = int.TryParse(req.QueryString["val"], out int val) ? val : 100;
                if (SetRoomBrightnessHandler != null)
                {
                    await SetRoomBrightnessHandler.Invoke(roomId, brightness);
                }
                responseJson = "{\"status\":\"ok\",\"brightness\":" + brightness + "}";
                statusCode = 200;
            }
        }
        catch (Exception ex)
        {
            responseJson = JsonSerializer.Serialize(new { status = "error", message = ex.Message });
            statusCode = 500;
        }

        byte[] buffer = Encoding.UTF8.GetBytes(responseJson);
        res.StatusCode = statusCode;
        res.ContentLength64 = buffer.Length;
        await res.OutputStream.WriteAsync(buffer);
        res.Close();
    }

    public void Dispose()
    {
        Stop();
        try { _listener.Close(); } catch { }
    }
}
