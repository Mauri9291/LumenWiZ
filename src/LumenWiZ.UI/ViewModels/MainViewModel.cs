using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using LumenWiZ.Core.Audio;
using LumenWiZ.Core.Models;
using LumenWiZ.Core.Network;
using LumenWiZ.Core.Services;
using LumenWiZ.Core.Storage;
using LumenWiZ.UI.Common;

namespace LumenWiZ.UI.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly DiscoveryService _discoveryService;
    private readonly SceneSnapshotService _snapshotService;
    private readonly AudioSyncService _audioSyncService;
    private readonly WizUdpTransport _transport;
    private readonly ConfigStorage _configStorage;

    private WizRoom? _selectedRoom;
    private bool _isScanning;
    private bool _isAudioSyncActive;
    private int _selectedNavIndex = 0;
    private string _statusMessage = "Listo";
    private string _newSceneName = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<WizRoom> Rooms { get; } = new();
    public ObservableCollection<DeviceItemViewModel> Devices { get; } = new();
    public ObservableCollection<WizCustomScene> CustomScenes { get; } = new();

    public WizRoom? SelectedRoom
    {
        get => _selectedRoom;
        set
        {
            if (_selectedRoom != value)
            {
                _selectedRoom = value;
                OnPropertyChanged();
                PopulateDevicesForSelectedRoom();
            }
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        set { _isScanning = value; OnPropertyChanged(); }
    }

    public bool IsAudioSyncActive
    {
        get => _isAudioSyncActive;
        set { _isAudioSyncActive = value; OnPropertyChanged(); }
    }

    public int SelectedNavIndex
    {
        get => _selectedNavIndex;
        set { _selectedNavIndex = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public string NewSceneName
    {
        get => _newSceneName;
        set { _newSceneName = value; OnPropertyChanged(); }
    }

    public ICommand ScanDevicesCommand { get; }
    public ICommand CaptureCurrentSceneCommand { get; }
    public ICommand ApplySceneCommand { get; }
    public ICommand ToggleAudioSyncCommand { get; }
    public ICommand NavigateCommand { get; }

    public MainViewModel(
        DiscoveryService discoveryService,
        SceneSnapshotService snapshotService,
        AudioSyncService audioSyncService,
        WizUdpTransport transport,
        ConfigStorage configStorage)
    {
        _discoveryService = discoveryService;
        _snapshotService = snapshotService;
        _audioSyncService = audioSyncService;
        _transport = transport;
        _configStorage = configStorage;

        ScanDevicesCommand = new RelayCommand(async () => await ScanDevicesAsync());
        CaptureCurrentSceneCommand = new RelayCommand(async () => await CaptureSceneAsync());
        ApplySceneCommand = new RelayCommand(async param =>
        {
            if (param is WizCustomScene scene)
            {
                await ApplySceneAsync(scene);
            }
        });
        ToggleAudioSyncCommand = new RelayCommand(() => ToggleAudioSync());
        NavigateCommand = new RelayCommand(param =>
        {
            if (int.TryParse(param?.ToString(), out int index))
            {
                SelectedNavIndex = index;
            }
        });
    }

    public async Task InitializeAsync()
    {
        var config = await _configStorage.LoadConfigAsync();
        CustomScenes.Clear();
        foreach (var sc in config.CustomScenes)
        {
            CustomScenes.Add(sc);
        }

        await ScanDevicesAsync();
    }

    public async Task ScanDevicesAsync()
    {
        IsScanning = true;
        StatusMessage = "Buscando luces WiZ en la red local...";
        try
        {
            var devices = await _discoveryService.DiscoverDevicesAsync(1500);
            var groupedRooms = DiscoveryService.GroupDevicesByRoom(devices);

            Rooms.Clear();
            foreach (var r in groupedRooms)
            {
                Rooms.Add(r);
            }

            SelectedRoom = Rooms.FirstOrDefault();
            StatusMessage = $"{devices.Count} luces encontradas en {Rooms.Count} habitación(es)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al buscar luces: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void PopulateDevicesForSelectedRoom()
    {
        Devices.Clear();
        if (SelectedRoom == null) return;

        foreach (var dev in SelectedRoom.Devices)
        {
            Devices.Add(new DeviceItemViewModel(dev, _transport));
        }
    }

    public async Task CaptureSceneAsync()
    {
        if (SelectedRoom == null) return;

        string name = string.IsNullOrWhiteSpace(NewSceneName) 
            ? $"Escena {DateTime.Now:HH:mm}" 
            : NewSceneName.Trim();

        StatusMessage = $"Capturando estado de {SelectedRoom.Name}...";
        try
        {
            var scene = await _snapshotService.CaptureLiveRoomSceneAsync(SelectedRoom, name, "📸");
            CustomScenes.Remove(CustomScenes.FirstOrDefault(s => s.Name == name && s.RoomId == SelectedRoom.RoomId)!);
            CustomScenes.Add(scene);
            NewSceneName = string.Empty;
            StatusMessage = $"Escena '{name}' guardada correctamente.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al capturar escena: {ex.Message}";
        }
    }

    public async Task ApplySceneAsync(WizCustomScene scene)
    {
        if (SelectedRoom == null) return;

        StatusMessage = $"Aplicando escena '{scene.Name}'...";
        try
        {
            await _snapshotService.ApplySceneAsync(scene, SelectedRoom.Devices);
            // Refresh device states in view models
            PopulateDevicesForSelectedRoom();
            StatusMessage = $"Escena '{scene.Name}' aplicada.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al aplicar escena: {ex.Message}";
        }
    }

    public void ToggleAudioSync()
    {
        if (SelectedRoom == null) return;

        if (_audioSyncService.IsRunning)
        {
            _audioSyncService.Stop();
            IsAudioSyncActive = false;
            StatusMessage = "Sincronización de audio detenida.";
        }
        else
        {
            _audioSyncService.Start(SelectedRoom.Devices);
            IsAudioSyncActive = true;
            StatusMessage = "Sincronización de audio WASAPI activa (25 FPS).";
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? prop = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
