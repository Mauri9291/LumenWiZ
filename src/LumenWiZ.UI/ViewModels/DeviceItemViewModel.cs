using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using LumenWiZ.Core.Models;
using LumenWiZ.Core.Network;
using LumenWiZ.Core.Protocol;
using LumenWiZ.Core.Storage;
using LumenWiZ.UI.Common;

namespace LumenWiZ.UI.ViewModels;

public class DeviceItemViewModel : INotifyPropertyChanged
{
    private readonly WizDevice _device;
    private readonly WizUdpTransport _transport;
    private readonly ConfigStorage? _configStorage;
    private readonly Action<DeviceItemViewModel, string>? _onRoomChanged;

    private string _assignedRoom = "Mi Habitación";
    public ObservableCollection<string> AvailableRooms { get; } = new();

    public string AssignedRoom
    {
        get => _assignedRoom;
        set
        {
            if (_assignedRoom != value && !string.IsNullOrWhiteSpace(value))
            {
                _assignedRoom = value;
                OnPropertyChanged();
                SaveRoomAssignmentAsync(value);
                _onRoomChanged?.Invoke(this, value);
            }
        }
    }

    public void UpdateAvailableRooms(IEnumerable<string> rooms)
    {
        AvailableRooms.Clear();
        foreach (var r in rooms)
        {
            AvailableRooms.Add(r);
        }
    }

    private async void SaveRoomAssignmentAsync(string roomName)
    {
        if (_configStorage != null)
        {
            var cfg = await _configStorage.LoadConfigAsync();
            cfg.DeviceRoomAssignments[Mac] = roomName;
            await _configStorage.SaveConfigAsync(cfg);
        }
    }

    private bool _isEditingName;
    private string _editNameText = string.Empty;
    private bool _isColorPickerOpen;
    private int _hue = 45;
    private byte _redValue = 255;
    private byte _greenValue = 180;
    private byte _blueValue = 80;
    private string _hexInput = "#FFB450";

    public event PropertyChangedEventHandler? PropertyChanged;

    public WizDevice Device => _device;
    public string Mac => _device.Mac;
    public string IpAddress => _device.IpAddress;
    public bool IsLightStrip => _device.IsLightStrip;
    public string DeviceTypeTag => IsLightStrip ? "(Tira LED)" : "(Foco)";

    public bool IsEditingName
    {
        get => _isEditingName;
        set { _isEditingName = value; OnPropertyChanged(); }
    }

    public string EditNameText
    {
        get => _editNameText;
        set { _editNameText = value; OnPropertyChanged(); }
    }

    public bool IsColorPickerOpen
    {
        get => _isColorPickerOpen;
        set { _isColorPickerOpen = value; OnPropertyChanged(); }
    }

    public int Hue
    {
        get => _hue;
        set
        {
            if (_hue != value)
            {
                _hue = value;
                OnPropertyChanged();
                var (r, g, b) = HsvToRgb(_hue, 1.0, 1.0);
                SetRgb(r, g, b);
            }
        }
    }

    public byte RedValue
    {
        get => _redValue;
        set { _redValue = value; OnPropertyChanged(); }
    }

    public byte GreenValue
    {
        get => _greenValue;
        set { _greenValue = value; OnPropertyChanged(); }
    }

    public byte BlueValue
    {
        get => _blueValue;
        set { _blueValue = value; OnPropertyChanged(); }
    }

    public string HexInput
    {
        get => _hexInput;
        set { _hexInput = value; OnPropertyChanged(); }
    }

    public string AudioBand
    {
        get => string.IsNullOrEmpty(_device.AudioBand) ? (IsLightStrip ? "Bass" : "Mids") : _device.AudioBand;
        set
        {
            _device.AudioBand = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AudioBandTag));
            OnPropertyChanged(nameof(IsBassBand));
            OnPropertyChanged(nameof(IsMidsBand));
            OnPropertyChanged(nameof(IsTrebleBand));
            OnPropertyChanged(nameof(IsFullBand));
            SaveBandConfigAsync(value);
        }
    }

    public bool IsBassBand => AudioBand == "Bass";
    public bool IsMidsBand => AudioBand == "Mids";
    public bool IsTrebleBand => AudioBand == "Treble";
    public bool IsFullBand => AudioBand == "Full";

    public string AudioBandTag => AudioBand switch
    {
        "Bass" => "Graves (Bass)",
        "Mids" => "Medios (Mids)",
        "Treble" => "Agudos (Treble)",
        "Full" => "Espectro Completo",
        _ => AudioBand
    };

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
            if (AudioSyncEnabled) return $"🎵 Reactivo: {AudioBandTag}";
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
                SendPilot(new WizPilotState { State = value });
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
                SendPilot(new WizPilotState { State = true, Dimming = value });
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
                int temp = _device.CurrentState.Temp.Value;
                if (temp <= 2700) return "#FBBF24";
                if (temp <= 4000) return "#FEF08A";
                return "#E0F2FE";
            }
            return "#F59E0B";
        }
    }

    public ICommand TogglePowerCommand { get; }
    public ICommand ToggleAudioSyncCommand { get; }
    public ICommand SetColorPresetCommand { get; }
    public ICommand StartEditNameCommand { get; }
    public ICommand SaveEditNameCommand { get; }
    public ICommand CancelEditNameCommand { get; }
    public ICommand ToggleColorPickerCommand { get; }
    public ICommand ApplyCustomRgbCommand { get; }
    public ICommand ApplyHexCommand { get; }
    public ICommand SetAudioBandCommand { get; }

    public DeviceItemViewModel(
        WizDevice device, 
        WizUdpTransport transport, 
        ConfigStorage? configStorage = null,
        string? initialRoom = null,
        IEnumerable<string>? availableRooms = null,
        Action<DeviceItemViewModel, string>? onRoomChanged = null)
    {
        _device = device;
        _transport = transport;
        _configStorage = configStorage;
        _onRoomChanged = onRoomChanged;

        if (!string.IsNullOrWhiteSpace(initialRoom))
        {
            _assignedRoom = initialRoom;
        }

        if (availableRooms != null)
        {
            foreach (var r in availableRooms)
            {
                AvailableRooms.Add(r);
            }
        }

        if (_device.CurrentState.R.HasValue && _device.CurrentState.G.HasValue && _device.CurrentState.B.HasValue)
        {
            _redValue = (byte)_device.CurrentState.R.Value;
            _greenValue = (byte)_device.CurrentState.G.Value;
            _blueValue = (byte)_device.CurrentState.B.Value;
            _hexInput = $"#{_redValue:X2}{_greenValue:X2}{_blueValue:X2}";
        }

        TogglePowerCommand = new RelayCommand(() => IsOn = !IsOn);
        ToggleAudioSyncCommand = new RelayCommand(() => AudioSyncEnabled = !AudioSyncEnabled);
        ToggleColorPickerCommand = new RelayCommand(() => IsColorPickerOpen = !IsColorPickerOpen);

        StartEditNameCommand = new RelayCommand(() =>
        {
            EditNameText = Name;
            IsEditingName = true;
        });

        SaveEditNameCommand = new RelayCommand(async () =>
        {
            if (!string.IsNullOrWhiteSpace(EditNameText))
            {
                Name = EditNameText.Trim();
                if (_configStorage != null)
                {
                    var cfg = await _configStorage.LoadConfigAsync();
                    cfg.DeviceNames[Mac] = Name;
                    await _configStorage.SaveConfigAsync(cfg);
                }
            }
            IsEditingName = false;
        });

        CancelEditNameCommand = new RelayCommand(() => IsEditingName = false);

        ApplyCustomRgbCommand = new RelayCommand(() =>
        {
            SetRgb(RedValue, GreenValue, BlueValue);
        });

        ApplyHexCommand = new RelayCommand(() =>
        {
            string hex = (HexInput ?? "").Trim().TrimStart('#');
            if (hex.Length == 6 &&
                byte.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out byte r) &&
                byte.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out byte g) &&
                byte.TryParse(hex[4..6], System.Globalization.NumberStyles.HexNumber, null, out byte b))
            {
                RedValue = r;
                GreenValue = g;
                BlueValue = b;
                SetRgb(r, g, b);
            }
        });

        SetColorPresetCommand = new RelayCommand(param =>
        {
            if (param is string preset)
            {
                ApplyPreset(preset);
            }
        });

        SetAudioBandCommand = new RelayCommand(param =>
        {
            if (param is string band)
            {
                AudioBand = band;
            }
        });
    }

    private async void SaveBandConfigAsync(string band)
    {
        if (_configStorage != null)
        {
            var cfg = await _configStorage.LoadConfigAsync();
            cfg.DeviceAudioBands[Mac] = band;
            await _configStorage.SaveConfigAsync(cfg);
        }
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
        SendPilot(new WizPilotState { State = true, Temp = kelvin, Dimming = Brightness });
    }

    public void SetRgb(byte r, byte g, byte b)
    {
        _redValue = r;
        _greenValue = g;
        _blueValue = b;
        _hexInput = $"#{r:X2}{g:X2}{b:X2}";
        OnPropertyChanged(nameof(RedValue));
        OnPropertyChanged(nameof(GreenValue));
        OnPropertyChanged(nameof(BlueValue));
        OnPropertyChanged(nameof(HexInput));

        _device.CurrentState.R = r;
        _device.CurrentState.G = g;
        _device.CurrentState.B = b;
        _device.CurrentState.Temp = null;
        OnPropertyChanged(nameof(ColorHex));
        OnPropertyChanged(nameof(Subtitle));
        SendPilot(new WizPilotState { State = true, R = r, G = g, B = b, Dimming = Brightness });
    }

    private void SendPilot(WizPilotState pilot)
    {
        if (IPAddress.TryParse(_device.IpAddress, out var ip))
        {
            string payload = WizProtocol.CreateSetPilotPayload(pilot);
            _transport.SendFireAndForget(ip, payload);
        }
    }

    public static (byte R, byte G, byte B) HsvToRgb(double hue, double saturation, double value)
    {
        int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
        double f = hue / 60 - Math.Floor(hue / 60);

        value = value * 255;
        byte v = Convert.ToByte(Math.Clamp(value, 0, 255));
        byte p = Convert.ToByte(Math.Clamp(value * (1 - saturation), 0, 255));
        byte q = Convert.ToByte(Math.Clamp(value * (1 - f * saturation), 0, 255));
        byte t = Convert.ToByte(Math.Clamp(value * (1 - (1 - f) * saturation), 0, 255));

        return hi switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q)
        };
    }

    protected void OnPropertyChanged([CallerMemberName] string? prop = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
