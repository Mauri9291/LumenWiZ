using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using LumenWiZ.Core.Models;
using LumenWiZ.Core.Network;
using LumenWiZ.Core.Protocol;
using LumenWiZ.UI.Common;

namespace LumenWiZ.UI.ViewModels;

public class DeviceItemViewModel : INotifyPropertyChanged
{
    private readonly WizDevice _device;
    private readonly WizUdpTransport _transport;

    public event PropertyChangedEventHandler? PropertyChanged;

    public WizDevice Device => _device;
    public string Mac => _device.Mac;
    public string IpAddress => _device.IpAddress;
    public bool IsLightStrip => _device.IsLightStrip;

    public string Name
    {
        get => string.IsNullOrEmpty(_device.Name) 
            ? (_device.IsLightStrip ? "Tira LED Escritorio" : $"Foco {_device.Mac.Substring(Math.Max(0, _device.Mac.Length - 4))}")
            : _device.Name;
        set
        {
            _device.Name = value;
            OnPropertyChanged();
        }
    }

    public string Subtitle
    {
        get
        {
            if (!IsOn) return "Apagado";
            if (AudioSyncEnabled) return "🎵 Audio Sync Activo";
            if (_device.CurrentState.Temp.HasValue && _device.CurrentState.Temp.Value > 0)
                return $"Blanco {_device.CurrentState.Temp}K";
            if (_device.CurrentState.R.HasValue)
                return $"Color RGB ({_device.CurrentState.R},{_device.CurrentState.G},{_device.CurrentState.B})";
            return "Encendido";
        }
    }

    public bool IsOn
    {
        get => _device.CurrentState.State;
        set
        {
            if (_device.CurrentState.State != value)
            {
                _device.CurrentState.State = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Subtitle));
                SendPilotAsync(new WizPilotState { State = value });
            }
        }
    }

    public int Brightness
    {
        get => _device.CurrentState.Dimming ?? 80;
        set
        {
            if (_device.CurrentState.Dimming != value)
            {
                _device.CurrentState.Dimming = value;
                OnPropertyChanged();
                SendPilotAsync(new WizPilotState { State = true, Dimming = value });
            }
        }
    }

    public bool AudioSyncEnabled
    {
        get => _device.AudioSyncEnabled;
        set
        {
            if (_device.AudioSyncEnabled != value)
            {
                _device.AudioSyncEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Subtitle));
            }
        }
    }

    public string ColorHex
    {
        get
        {
            if (_device.CurrentState.R.HasValue && _device.CurrentState.G.HasValue && _device.CurrentState.B.HasValue)
            {
                return $"#{_device.CurrentState.R.Value:X2}{_device.CurrentState.G.Value:X2}{_device.CurrentState.B.Value:X2}";
            }
            if (_device.CurrentState.Temp.HasValue)
            {
                // Approximate warm white to cool white
                int temp = _device.CurrentState.Temp.Value;
                if (temp <= 2700) return "#FBBF24"; // warm amber
                if (temp <= 4000) return "#FEF08A"; // soft warm
                return "#E0F2FE"; // cool daylight
            }
            return "#F59E0B";
        }
    }

    public ICommand TogglePowerCommand { get; }
    public ICommand ToggleAudioSyncCommand { get; }
    public ICommand SetColorPresetCommand { get; }

    public DeviceItemViewModel(WizDevice device, WizUdpTransport transport)
    {
        _device = device;
        _transport = transport;

        TogglePowerCommand = new RelayCommand(() => IsOn = !IsOn);
        ToggleAudioSyncCommand = new RelayCommand(() => AudioSyncEnabled = !AudioSyncEnabled);
        SetColorPresetCommand = new RelayCommand(param =>
        {
            if (param is string preset)
            {
                ApplyPreset(preset);
            }
        });
    }

    public void ApplyPreset(string preset)
    {
        switch (preset.ToLowerInvariant())
        {
            case "warm":
                SetTemperature(2700);
                break;
            case "daylight":
                SetTemperature(4000);
                break;
            case "cool":
                SetTemperature(6500);
                break;
            case "purple":
                SetRgb(168, 85, 247);
                break;
            case "blue":
                SetRgb(59, 130, 246);
                break;
            case "green":
                SetRgb(16, 185, 129);
                break;
            case "amber":
                SetRgb(245, 158, 11);
                break;
            case "red":
                SetRgb(239, 68, 68);
                break;
        }
    }

    public void SetTemperature(int kelvin)
    {
        _device.CurrentState.Temp = kelvin;
        _device.CurrentState.R = null;
        _device.CurrentState.G = null;
        _device.CurrentState.B = null;
        OnPropertyChanged(nameof(ColorHex));
        OnPropertyChanged(nameof(Subtitle));
        SendPilotAsync(new WizPilotState { State = true, Temp = kelvin, Dimming = Brightness });
    }

    public void SetRgb(byte r, byte g, byte b)
    {
        _device.CurrentState.R = r;
        _device.CurrentState.G = g;
        _device.CurrentState.B = b;
        _device.CurrentState.Temp = null;
        OnPropertyChanged(nameof(ColorHex));
        OnPropertyChanged(nameof(Subtitle));
        SendPilotAsync(new WizPilotState { State = true, R = r, G = g, B = b, Dimming = Brightness });
    }

    private async void SendPilotAsync(WizPilotState pilot)
    {
        if (IPAddress.TryParse(_device.IpAddress, out var ip))
        {
            string payload = WizProtocol.CreateSetPilotPayload(pilot);
            await _transport.SendAsync(ip, payload, 30);
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? prop = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
