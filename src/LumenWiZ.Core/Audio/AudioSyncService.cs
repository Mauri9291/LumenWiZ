using System.Net;
using LumenWiZ.Core.Models;
using LumenWiZ.Core.Network;
using LumenWiZ.Core.Protocol;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace LumenWiZ.Core.Audio;

public class AudioDeviceInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }

    public string DisplayName => Name + (IsDefault ? " (Predeterminado)" : "");
    public override string ToString() => DisplayName;
}

public class AudioSyncService : IDisposable
{
    private readonly WizUdpTransport _transport;
    private WasapiLoopbackCapture? _capture;
    private AudioSpectrumAnalyzer? _analyzer;
    private List<WizDevice> _activeDevices = new();
    private readonly object _lock = new();
    private bool _isRunning;
    private DateTime _lastDispatch = DateTime.MinValue;
    private int _isTransmitting;
    private string? _selectedDeviceId;
    private const int MinDispatchIntervalMs = 70; // 14 frames per second

    public bool IsRunning => _isRunning;
    public string? SelectedDeviceId => _selectedDeviceId;

    public event Action<float, float, float>? AudioEnergyUpdated;

    public AudioSyncService(WizUdpTransport transport)
    {
        _transport = transport;
    }

    public static List<AudioDeviceInfo> GetAvailableAudioDevices()
    {
        var list = new List<AudioDeviceInfo>();
        try
        {
            var enumerator = new MMDeviceEnumerator();
            string defaultId = "";
            try
            {
                var def = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                defaultId = def.ID;
            }
            catch { }

            foreach (var endpoint in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                list.Add(new AudioDeviceInfo
                {
                    Id = endpoint.ID,
                    Name = endpoint.FriendlyName,
                    IsDefault = endpoint.ID == defaultId
                });
            }
        }
        catch { }
        return list;
    }

    public void Start(IEnumerable<WizDevice> devices, string? deviceId = null)
    {
        lock (_lock)
        {
            if (_isRunning) Stop();

            _selectedDeviceId = deviceId;
            _activeDevices = devices.Where(d => d.AudioSyncEnabled && d.IsOnline).ToList();
            if (_activeDevices.Count == 0) return;

            try
            {
                if (!string.IsNullOrEmpty(deviceId))
                {
                    try
                    {
                        var enumerator = new MMDeviceEnumerator();
                        var dev = enumerator.GetDevice(deviceId);
                        _capture = new WasapiLoopbackCapture(dev);
                    }
                    catch
                    {
                        _capture = new WasapiLoopbackCapture();
                    }
                }
                else
                {
                    _capture = new WasapiLoopbackCapture();
                }

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

    public void UpdateDevices(IEnumerable<WizDevice> devices)
    {
        lock (_lock)
        {
            _activeDevices = devices.Where(d => d.AudioSyncEnabled && d.IsOnline).ToList();
        }
    }

    private void OnAudioDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (!_isRunning || _analyzer == null || e.BytesRecorded == 0) return;

        if ((DateTime.UtcNow - _lastDispatch).TotalMilliseconds < MinDispatchIntervalMs)
            return;

        if (Interlocked.CompareExchange(ref _isTransmitting, 1, 0) != 0)
            return; // Drop frame if previous is still in-flight

        _lastDispatch = DateTime.UtcNow;

        try
        {
            float[] samples;
            int bits = _capture?.WaveFormat.BitsPerSample ?? 32;

            if (bits == 32)
            {
                int sampleCount = e.BytesRecorded / 4;
                samples = new float[sampleCount];
                for (int i = 0; i < sampleCount; i++)
                {
                    samples[i] = BitConverter.ToSingle(e.Buffer, i * 4);
                }
            }
            else if (bits == 16)
            {
                int sampleCount = e.BytesRecorded / 2;
                samples = new float[sampleCount];
                for (int i = 0; i < sampleCount; i++)
                {
                    short val = BitConverter.ToInt16(e.Buffer, i * 2);
                    samples[i] = val / 32768.0f;
                }
            }
            else
            {
                return;
            }

            var (bass, mid, treble) = _analyzer.AnalyzeBandEnergies(samples);
            AudioEnergyUpdated?.Invoke(bass, mid, treble);

            List<WizDevice> targets;
            lock (_lock)
            {
                targets = _activeDevices.ToList();
            }

            foreach (var device in targets)
            {
                if (!IPAddress.TryParse(device.IpAddress, out var ip)) continue;

                string band = string.IsNullOrEmpty(device.AudioBand)
                    ? (device.IsLightStrip ? "Bass" : "Mids")
                    : device.AudioBand;

                WizPilotState reactiveState;

                switch (band.ToLowerInvariant())
                {
                    case "bass":
                        // Bass: deep beat pulsation
                        int bassBright = Math.Clamp((int)(20 + (bass * 80)), 10, 100);
                        reactiveState = new WizPilotState
                        {
                            State = true,
                            Dimming = bassBright,
                            R = Math.Clamp((int)(90 + (bass * 165)), 0, 255),
                            G = 0,
                            B = Math.Clamp((int)(140 + (bass * 115)), 0, 255)
                        };
                        break;

                    case "treble":
                        // Treble: gold sparkle & bright ice accents
                        int trebBright = Math.Clamp((int)(15 + (treble * 85)), 10, 100);
                        reactiveState = new WizPilotState
                        {
                            State = true,
                            Dimming = trebBright,
                            R = Math.Clamp((int)(220 + (treble * 35)), 0, 255),
                            G = Math.Clamp((int)(180 + (treble * 75)), 0, 255),
                            B = Math.Clamp((int)(treble * 240), 0, 255)
                        };
                        break;

                    case "full":
                        // Full spectrum: complete RGB energy blend
                        int fullBright = Math.Clamp((int)(25 + (bass * 45) + (mid * 30)), 10, 100);
                        reactiveState = new WizPilotState
                        {
                            State = true,
                            Dimming = fullBright,
                            R = Math.Clamp((int)(bass * 255), 0, 255),
                            G = Math.Clamp((int)(mid * 255), 0, 255),
                            B = Math.Clamp((int)(treble * 255), 0, 255)
                        };
                        break;

                    case "mids":
                    default:
                        // Mids: harmonic melody, emerald to deep sapphire blue
                        int midBright = Math.Clamp((int)(20 + (mid * 80)), 10, 100);
                        reactiveState = new WizPilotState
                        {
                            State = true,
                            Dimming = midBright,
                            R = 0,
                            G = Math.Clamp((int)(120 + (mid * 135)), 0, 255),
                            B = Math.Clamp((int)(160 + (treble * 95)), 0, 255)
                        };
                        break;
                }

                string payload = WizProtocol.CreateSetPilotPayload(reactiveState);
                _transport.SendFireAndForget(ip, payload);
            }
        }
        catch { }
        finally
        {
            Interlocked.Exchange(ref _isTransmitting, 0);
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
