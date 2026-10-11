using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using LumenWiZ.Core.Audio;
using LumenWiZ.Core.Models;
using LumenWiZ.Core.Network;
using LumenWiZ.Core.Protocol;
using LumenWiZ.Core.Services;

namespace LumenWiZ.UI.ViewModels;

public class TrayPopoverViewModel : INotifyPropertyChanged
{
    private readonly WizUdpTransport _transport;
    private readonly DiscoveryService _discoveryService;
    private readonly SceneSnapshotService _snapshotService;
    private readonly AudioSyncService _audioSyncService;

    private WizRoom? _activeRoom;
    private int _brightness = 80;
    private bool _isMasterOn = true;
    private string _activeSceneName = "Luz Diaria";
    private bool _isAudioSyncActive;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? OpenSettingsRequested;

    public WizRoom? ActiveRoom
    {
        get => _activeRoom;
        set { _activeRoom = value; OnPropertyChanged(); OnPropertyChanged(nameof(RoomStatusText)); }
    }

    public int Brightness
    {
        get => _brightness;
        set
        {
            if (_brightness != value)
            {
                _brightness = value;
                OnPropertyChanged();
                ApplyRoomBrightnessAsync(value);
            }
        }
    }

    public bool IsMasterOn
    {
        get => _isMasterOn;
        set
        {
            if (_isMasterOn != value)
            {
                _isMasterOn = value;
                OnPropertyChanged();
                ToggleRoomPowerAsync(value);
            }
        }
    }

    public string ActiveSceneName
    {
        get => _activeSceneName;
        set { _activeSceneName = value; OnPropertyChanged(); }
    }

    public bool IsAudioSyncActive
    {
        get => _isAudioSyncActive;
        set { _isAudioSyncActive = value; OnPropertyChanged(); }
    }

    public string RoomStatusText
    {
        get
        {
            if (ActiveRoom == null) return "Buscando luces...";
            int count = ActiveRoom.Devices.Count;
            return $"{count} {(count == 1 ? "luz activa" : "luces activas")}";
        }
    }

    public ObservableCollection<WizRoom> AvailableRooms { get; } = new();

    public System.Windows.Input.ICommand OpenSettingsCommand { get; }
    public System.Windows.Input.ICommand ToggleAudioSyncCommand { get; }
    public System.Windows.Input.ICommand SetSceneCommand { get; }
    public System.Windows.Input.ICommand TogglePowerCommand { get; }

    public TrayPopoverViewModel(
        WizUdpTransport transport, 
        DiscoveryService discoveryService, 
        SceneSnapshotService snapshotService,
        AudioSyncService audioSyncService)
    {
        _transport = transport;
        _discoveryService = discoveryService;
        _snapshotService = snapshotService;
        _audioSyncService = audioSyncService;

        OpenSettingsCommand = new LumenWiZ.UI.Common.RelayCommand(() => RequestOpenSettings());
        ToggleAudioSyncCommand = new LumenWiZ.UI.Common.RelayCommand(() => ToggleAudioSync());
        SetSceneCommand = new LumenWiZ.UI.Common.RelayCommand(param =>
        {
            if (param is string sceneStr)
            {
                var parts = sceneStr.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out int scId))
                {
                    _ = SetSceneAsync(parts[0], scId);
                }
            }
        });
        TogglePowerCommand = new LumenWiZ.UI.Common.RelayCommand(() => IsMasterOn = !IsMasterOn);
    }

    public async Task RefreshStateAsync()
    {
        var devices = await _discoveryService.DiscoverDevicesAsync(1500);
        var rooms = DiscoveryService.GroupDevicesByRoom(devices);

        AvailableRooms.Clear();
        foreach (var r in rooms)
        {
            AvailableRooms.Add(r);
        }

        ActiveRoom = AvailableRooms.FirstOrDefault();
        if (ActiveRoom != null)
        {
            _brightness = ActiveRoom.MasterBrightness;
            _isMasterOn = ActiveRoom.IsOn;
            OnPropertyChanged(nameof(Brightness));
            OnPropertyChanged(nameof(IsMasterOn));
        }
    }

    public async Task SetSceneAsync(string sceneName, int sceneId)
    {
        ActiveSceneName = sceneName;
        if (ActiveRoom == null) return;

        // Turn off audio sync if switching scenes
        if (_audioSyncService.IsRunning)
        {
            _audioSyncService.Stop();
            IsAudioSyncActive = false;
        }

        var pilotState = new WizPilotState
        {
            State = true,
            SceneId = sceneId,
            Dimming = _brightness
        };

        string payload = WizProtocol.CreateSetPilotPayload(pilotState);

        foreach (var device in ActiveRoom.Devices)
        {
            if (IPAddress.TryParse(device.IpAddress, out var ip))
            {
                await _transport.SendAsync(ip, payload, 30);
            }
        }
    }

    public void ToggleAudioSync()
    {
        if (ActiveRoom == null) return;

        if (_audioSyncService.IsRunning)
        {
            _audioSyncService.Stop();
            IsAudioSyncActive = false;
        }
        else
        {
            _audioSyncService.Start(ActiveRoom.Devices);
            IsAudioSyncActive = true;
            ActiveSceneName = "Audio Sync";
        }
    }

    private async void ApplyRoomBrightnessAsync(int value)
    {
        if (ActiveRoom == null) return;

        var pilotState = new WizPilotState
        {
            State = true,
            Dimming = value
        };
        string payload = WizProtocol.CreateSetPilotPayload(pilotState);

        foreach (var device in ActiveRoom.Devices)
        {
            if (IPAddress.TryParse(device.IpAddress, out var ip))
            {
                await _transport.SendAsync(ip, payload, 30);
            }
        }
    }

    private async void ToggleRoomPowerAsync(bool power)
    {
        if (ActiveRoom == null) return;

        var pilotState = new WizPilotState { State = power };
        string payload = WizProtocol.CreateSetPilotPayload(pilotState);

        foreach (var device in ActiveRoom.Devices)
        {
            if (IPAddress.TryParse(device.IpAddress, out var ip))
            {
                await _transport.SendAsync(ip, payload, 30);
            }
        }
    }

    public void RequestOpenSettings()
    {
        OpenSettingsRequested?.Invoke();
    }

    protected void OnPropertyChanged([CallerMemberName] string? prop = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
