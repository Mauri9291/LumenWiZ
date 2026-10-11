using System.Net;
using LumenWiZ.Core.Models;
using LumenWiZ.Core.Network;
using LumenWiZ.Core.Protocol;
using NAudio.Wave;

namespace LumenWiZ.Core.Audio;

public class AudioSyncService : IDisposable
{
    private readonly WizUdpTransport _transport;
    private WasapiLoopbackCapture? _capture;
    private AudioSpectrumAnalyzer? _analyzer;
    private List<WizDevice> _activeDevices = new();
    private readonly object _lock = new();
    private bool _isRunning;
    private DateTime _lastDispatch = DateTime.MinValue;
    private const int MinDispatchIntervalMs = 40; // Max 25 frames per second

    public bool IsRunning => _isRunning;

    public event Action<float, float, float>? AudioEnergyUpdated;

    public AudioSyncService(WizUdpTransport transport)
    {
        _transport = transport;
    }

    public void Start(IEnumerable<WizDevice> devices)
    {
        lock (_lock)
        {
            if (_isRunning) return;

            _activeDevices = devices.Where(d => d.AudioSyncEnabled && d.IsOnline).ToList();
            if (_activeDevices.Count == 0) return;

            try
            {
                _capture = new WasapiLoopbackCapture();
                _analyzer = new AudioSpectrumAnalyzer(_capture.WaveFormat.SampleRate);

                _capture.DataAvailable += OnAudioDataAvailable;
                _capture.RecordingStopped += (s, e) => { _isRunning = false; };

                _capture.StartRecording();
                _isRunning = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioSync] Could not start WASAPI loopback: {ex.Message}");
                _isRunning = false;
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning) return;

            try
            {
                _capture?.StopRecording();
                _capture?.Dispose();
                _capture = null;
            }
            catch { }

            _isRunning = false;
        }
    }

    private void OnAudioDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (!_isRunning || _analyzer == null || e.BytesRecorded == 0) return;

        // Throttling: Ensure we do not exceed 25 UDP packets/sec per bulb
        if ((DateTime.UtcNow - _lastDispatch).TotalMilliseconds < MinDispatchIntervalMs)
            return;

        _lastDispatch = DateTime.UtcNow;

        // Convert byte buffer to float samples (IEEE Float 32-bit format typical in WASAPI Loopback)
        int sampleCount = e.BytesRecorded / 4;
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = BitConverter.ToSingle(e.Buffer, i * 4);
        }

        var (bass, mid, treble) = _analyzer.AnalyzeBandEnergies(samples);
        AudioEnergyUpdated?.Invoke(bass, mid, treble);

        // Send reactive updates to active devices asynchronously
        Task.Run(async () =>
        {
            foreach (var device in _activeDevices)
            {
                if (!IPAddress.TryParse(device.IpAddress, out var ip)) continue;

                WizPilotState reactiveState;
                if (device.IsLightStrip)
                {
                    // LED Strip: reacts strongly to bass with electric neon hue
                    int brightness = Math.Clamp((int)(30 + (bass * 70)), 10, 100);
                    int r = Math.Clamp((int)(bass * 255), 60, 255);
                    int g = Math.Clamp((int)(mid * 100), 0, 100);
                    int b = Math.Clamp((int)(treble * 255), 120, 255);

                    reactiveState = new WizPilotState
                    {
                        State = true,
                        Dimming = brightness,
                        R = r,
                        G = g,
                        B = b
                    };
                }
                else
                {
                    // Standard bulbs: smooth warm-to-cool or hue modulation based on mid/treble
                    int brightness = Math.Clamp((int)(40 + (mid * 60)), 10, 100);
                    reactiveState = new WizPilotState
                    {
                        State = true,
                        Dimming = brightness,
                        R = Math.Clamp((int)(180 + (bass * 75)), 0, 255),
                        G = Math.Clamp((int)(100 + (mid * 155)), 0, 255),
                        B = Math.Clamp((int)(50 + (treble * 205)), 0, 255)
                    };
                }

                string payload = WizProtocol.CreateSetPilotPayload(reactiveState);
                await _transport.SendAsync(ip, payload, 30);
            }
        });
    }

    public void Dispose()
    {
        Stop();
    }
}
