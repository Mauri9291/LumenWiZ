using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using LumenWiZ.Core.Audio;
using LumenWiZ.Core.Models;
using LumenWiZ.Core.Network;
using LumenWiZ.Core.Protocol;
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
    private bool _isRoomMasterOn = true;
    private int _roomMasterBrightness = 80;

    public event PropertyChangedEventHandler? PropertyChanged;

    public record LanguageOption(string Code, string DisplayName);

    public ObservableCollection<LanguageOption> AvailableLanguages { get; } = new()
    {
        new("es", "Español"),
        new("en", "English")
    };

    private string _selectedLanguage = "es";
    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (_selectedLanguage != value && !string.IsNullOrEmpty(value))
            {
                _selectedLanguage = value;
                OnPropertyChanged();
                LocalizationService.Instance.CurrentLanguage = value;
                LoadPredefinedScenes();
                _ = RefreshQuickSlotsAsync();
                OnPropertyChanged(nameof(AudioSyncButtonText));
                _ = SaveGeneralSettingsAsync();
            }
        }
    }

    private bool _startWithWindows = true;
    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (_startWithWindows != value)
            {
                _startWithWindows = value;
                OnPropertyChanged();
                WindowsStartupHelper.ApplyStartupConfig(_startWithWindows, _startMinimized);
                _ = SaveGeneralSettingsAsync();
            }
        }
    }

    private bool _startMinimized = true;
    public bool StartMinimized
    {
        get => _startMinimized;
        set
        {
            if (_startMinimized != value)
            {
                _startMinimized = value;
                OnPropertyChanged();
                if (_startWithWindows)
                {
                    WindowsStartupHelper.ApplyStartupConfig(_startWithWindows, _startMinimized);
                }
                _ = SaveGeneralSettingsAsync();
            }
        }
    }

    private async Task SaveGeneralSettingsAsync()
    {
        try
        {
            var cfg = await _configStorage.LoadConfigAsync();
            cfg.Language = _selectedLanguage;
            cfg.StartWithWindows = _startWithWindows;
            cfg.StartMinimized = _startMinimized;
            await _configStorage.SaveConfigAsync(cfg);
        }
        catch { }
    }

    public ObservableCollection<WizRoom> Rooms { get; } = new();
    public ObservableCollection<string> AvailableRoomNames { get; } = new();
    public ObservableCollection<DeviceItemViewModel> Devices { get; } = new();
    public ObservableCollection<PredefinedSceneViewModel> PredefinedScenes { get; } = new();
    public ObservableCollection<SceneItemViewModel> CustomScenes { get; } = new();
    public ObservableCollection<AudioDeviceInfo> AudioDevices { get; } = new();
    public ObservableCollection<QuickSlotConfigViewModel> QuickSlots { get; } = new();

    private bool _isAddingRoom;
    public bool IsAddingRoom
    {
        get => _isAddingRoom;
        set { _isAddingRoom = value; OnPropertyChanged(); }
    }

    private string _newRoomName = string.Empty;
    public string NewRoomName
    {
        get => _newRoomName;
        set { _newRoomName = value; OnPropertyChanged(); }
    }

    private int _selectedScenesTabIndex = 0;
    public int SelectedScenesTabIndex
    {
        get => _selectedScenesTabIndex;
        set { _selectedScenesTabIndex = value; OnPropertyChanged(); }
    }

    private string _newSceneIcon = "💡";
    public string NewSceneIcon
    {
        get => _newSceneIcon;
        set { _newSceneIcon = value; OnPropertyChanged(); }
    }

    private AudioDeviceInfo? _selectedAudioDevice;
    public AudioDeviceInfo? SelectedAudioDevice
    {
        get => _selectedAudioDevice;
        set
        {
            if (_selectedAudioDevice != value)
            {
                _selectedAudioDevice = value;
                OnPropertyChanged();
                if (_isAudioSyncActive && SelectedRoom != null)
                {
                    _audioSyncService.Stop();
                    _audioSyncService.Start(SelectedRoom.Devices, _selectedAudioDevice?.Id);
                    StatusMessage = $"Audio Reactivo cambiado a: {_selectedAudioDevice?.Name ?? "Predeterminado"}";
                }
            }
        }
    }

    public WizRoom? SelectedRoom
    {
        get => _selectedRoom;
        set
        {
            _selectedRoom = value;
            OnPropertyChanged();
            PopulateDevicesForSelectedRoom();
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        set { _isScanning = value; OnPropertyChanged(); }
    }

    public bool IsRoomMasterOn
    {
        get => _isRoomMasterOn;
        set
        {
            if (_isRoomMasterOn != value)
            {
                _isRoomMasterOn = value;
                OnPropertyChanged();
                ToggleAllRoomDevices(value);
            }
        }
    }

    public int RoomMasterBrightness
    {
        get => _roomMasterBrightness;
        set
        {
            if (_roomMasterBrightness != value)
            {
                _roomMasterBrightness = value;
                OnPropertyChanged();
                SetAllRoomBrightness(value);
            }
        }
    }

    public bool IsAudioSyncActive
    {
        get => _isAudioSyncActive;
        set 
        { 
            _isAudioSyncActive = value; 
            OnPropertyChanged(); 
            OnPropertyChanged(nameof(AudioSyncButtonText)); 
        }
    }

    public string AudioSyncButtonText => IsAudioSyncActive 
        ? LocalizationService.Tr("Loc_StopAudioSync") 
        : LocalizationService.Tr("Loc_StartAudioSync");

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
    public ICommand AssignDeviceBandCommand { get; }
    public ICommand RefreshAudioDevicesCommand { get; }
    public ICommand MoveDeviceUpCommand { get; }
    public ICommand MoveDeviceDownCommand { get; }
    public ICommand SelectNewSceneIconCommand { get; }
    public ICommand SelectScenesTabCommand { get; }
    public ICommand SelectRoomCommand { get; }
    public ICommand StartAddRoomCommand { get; }
    public ICommand CancelAddRoomCommand { get; }
    public ICommand ConfirmAddRoomCommand { get; }
    public ICommand DeleteRoomCommand { get; }

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
            if (param is SceneItemViewModel itemVm)
            {
                await ApplyCustomSceneAsync(itemVm.Model);
            }
            else if (param is WizCustomScene scene)
            {
                await ApplyCustomSceneAsync(scene);
            }
        });
        SelectNewSceneIconCommand = new RelayCommand(param =>
        {
            if (param is string icon)
            {
                NewSceneIcon = icon;
            }
        });
        ToggleAudioSyncCommand = new RelayCommand(() => ToggleAudioSync());
        NavigateCommand = new RelayCommand(param =>
        {
            if (param is string str && int.TryParse(str, out int idx))
            {
                SelectedNavIndex = idx;
            }
            else if (param is int i)
            {
                SelectedNavIndex = i;
            }
        });

        AssignDeviceBandCommand = new RelayCommand(param =>
        {
            if (param is object[] arr && arr.Length == 2 && arr[0] is DeviceItemViewModel dev && arr[1] is string band)
            {
                dev.AudioBand = band;
                if (_isAudioSyncActive && SelectedRoom != null)
                {
                    _audioSyncService.UpdateDevices(SelectedRoom.Devices);
                }
            }
        });

        RefreshAudioDevicesCommand = new RelayCommand(() => RefreshAudioDevices());

        MoveDeviceUpCommand = new RelayCommand(param =>
        {
            if (param is DeviceItemViewModel dev) MoveDeviceUp(dev);
        });

        MoveDeviceDownCommand = new RelayCommand(param =>
        {
            if (param is DeviceItemViewModel dev) MoveDeviceDown(dev);
        });

        SelectScenesTabCommand = new RelayCommand(param =>
        {
            if (param != null && int.TryParse(param.ToString(), out int tab))
            {
                SelectedScenesTabIndex = tab;
            }
        });

        SelectRoomCommand = new RelayCommand(param =>
        {
            if (param is WizRoom r)
            {
                SelectedRoom = r;
                SelectedNavIndex = 0;
            }
        });

        StartAddRoomCommand = new RelayCommand(() =>
        {
            IsAddingRoom = true;
            NewRoomName = string.Empty;
        });

        CancelAddRoomCommand = new RelayCommand(() =>
        {
            IsAddingRoom = false;
            NewRoomName = string.Empty;
        });

        ConfirmAddRoomCommand = new RelayCommand(async () => await ConfirmAddRoomAsync());

        DeleteRoomCommand = new RelayCommand(async param =>
        {
            if (param is string rName) await DeleteRoomAsync(rName);
            else if (param is WizRoom wr) await DeleteRoomAsync(wr.Name);
        });

        LocalizationService.Instance.LanguageChanged += () =>
        {
            RunOnUIThread(() =>
            {
                LoadPredefinedScenes();
                OnPropertyChanged(nameof(AudioSyncButtonText));
            });
        };
    }

    public async Task InitializeAsync()
    {
        string logFile = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LumenWiZ", "debug.log");
        System.IO.File.AppendAllText(logFile, "MainViewModel: InitializeAsync enter\n");

        var config = await _configStorage.LoadConfigAsync();
        _selectedLanguage = string.Equals(config.Language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "es";
        _startWithWindows = config.StartWithWindows;
        _startMinimized = config.StartMinimized;
        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(StartWithWindows));
        OnPropertyChanged(nameof(StartMinimized));
        LocalizationService.Instance.CurrentLanguage = _selectedLanguage;
        WindowsStartupHelper.ApplyStartupConfig(_startWithWindows, _startMinimized);

        System.IO.File.AppendAllText(logFile, "MainViewModel: Config loaded, updating CustomScenes...\n");
        RunOnUIThread(() =>
        {
            CustomScenes.Clear();
            foreach (var sc in config.CustomScenes)
            {
                CustomScenes.Add(new SceneItemViewModel(
                    sc,
                    _configStorage,
                    applyAction: async item => await ApplyCustomSceneAsync(item.Model),
                    deleteAction: async item => await DeleteCustomSceneAsync(item),
                    onChangedAction: () => OnCustomScenesChanged()
                ));
            }

            LoadPredefinedScenes();
        });

        await RefreshQuickSlotsAsync();

        System.IO.File.AppendAllText(logFile, "MainViewModel: Calling ScanDevicesAsync...\n");
        await ScanDevicesAsync();
        RefreshAudioDevices();
        System.IO.File.AppendAllText(logFile, "MainViewModel: InitializeAsync done\n");
    }

    public async Task ScanDevicesAsync()
    {
        string logFile = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LumenWiZ", "debug.log");
        System.IO.File.AppendAllText(logFile, "MainViewModel: ScanDevicesAsync enter\n");
        IsScanning = true;
        StatusMessage = "Buscando luces WiZ en la red local...";
        try
        {
            var config = await _configStorage.LoadConfigAsync();
            var customList = config.CustomRooms != null && config.CustomRooms.Count > 0 
                ? config.CustomRooms 
                : new List<string> { "Mi Habitación" };

            System.IO.File.AppendAllText(logFile, "MainViewModel: Calling DiscoverDevicesAsync(1800)...\n");
            var devices = await _discoveryService.DiscoverDevicesAsync(1800, config.DeviceNames, config.DeviceAudioBands);
            System.IO.File.AppendAllText(logFile, $"MainViewModel: DiscoverDevicesAsync returned {devices.Count} devices\n");
            var groupedRooms = DiscoveryService.GroupDevicesByRoom(
                devices, 
                config.DeviceRoomAssignments, 
                config.CustomRooms);
            System.IO.File.AppendAllText(logFile, $"MainViewModel: Grouped into {groupedRooms.Count} rooms\n");

            RunOnUIThread(() =>
            {
                System.IO.File.AppendAllText(logFile, "MainViewModel: Updating Rooms on UI thread...\n");
                AvailableRoomNames.Clear();
                foreach (var c in customList)
                {
                    AvailableRoomNames.Add(c);
                }

                Rooms.Clear();
                foreach (var r in groupedRooms)
                {
                    Rooms.Add(r);
                }

                SelectedRoom = Rooms.FirstOrDefault();
                (System.Windows.Application.Current as App)?.PopoverViewModel?.UpdateRooms(groupedRooms);
                System.IO.File.AppendAllText(logFile, $"MainViewModel: SelectedRoom set to {SelectedRoom?.Name}\n");
            });

            StatusMessage = $"{devices.Count} luces encontradas en {Rooms.Count} habitación(es)";
        }
        catch (Exception ex)
        {
            System.IO.File.AppendAllText(logFile, $"MainViewModel: ScanDevicesAsync EXCEPTION: {ex}\n");
            StatusMessage = $"Error al buscar luces: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private async void PopulateDevicesForSelectedRoom()
    {
        var config = await _configStorage.LoadConfigAsync();

        RunOnUIThread(() =>
        {
            Devices.Clear();
            if (SelectedRoom == null) return;

            var devList = SelectedRoom.Devices.ToList();
            if (config.DeviceOrder != null && config.DeviceOrder.Count > 0)
            {
                devList = devList
                    .OrderBy(d =>
                    {
                        int idx = config.DeviceOrder.IndexOf(d.Mac);
                        return idx >= 0 ? idx : int.MaxValue;
                    })
                    .ToList();
            }

            foreach (var dev in devList)
            {
                string assigned = (config.DeviceRoomAssignments != null && config.DeviceRoomAssignments.TryGetValue(dev.Mac, out var r) && !string.IsNullOrWhiteSpace(r))
                    ? r
                    : (AvailableRoomNames.FirstOrDefault() ?? "Mi Habitación");

                Devices.Add(new DeviceItemViewModel(
                    dev, 
                    _transport, 
                    _configStorage, 
                    initialRoom: assigned, 
                    availableRooms: AvailableRoomNames, 
                    onRoomChanged: OnDeviceRoomChanged));
            }

            _isRoomMasterOn = SelectedRoom.Devices.Any(d => d.CurrentState.State);
            var onDevs = SelectedRoom.Devices.Where(d => d.CurrentState.State).ToList();
            if (onDevs.Count > 0)
            {
                _roomMasterBrightness = (int)onDevs.Average(d => d.CurrentState.Dimming ?? 80);
            }
            OnPropertyChanged(nameof(IsRoomMasterOn));
            OnPropertyChanged(nameof(RoomMasterBrightness));
        });
    }

    private async void OnDeviceRoomChanged(DeviceItemViewModel devVm, string newRoom)
    {
        try
        {
            var config = await _configStorage.LoadConfigAsync();
            config.DeviceRoomAssignments[devVm.Mac] = newRoom;
            await _configStorage.SaveConfigAsync(config);

            var allDevices = Rooms.SelectMany(r => r.Devices).DistinctBy(d => d.Mac).ToList();
            var groupedRooms = DiscoveryService.GroupDevicesByRoom(
                allDevices, 
                config.DeviceRoomAssignments, 
                config.CustomRooms);

            string currentSelectedName = SelectedRoom?.Name ?? "General";
            RunOnUIThread(() =>
            {
                Rooms.Clear();
                foreach (var r in groupedRooms)
                {
                    Rooms.Add(r);
                }
                SelectedRoom = Rooms.FirstOrDefault(r => r.Name == currentSelectedName) ?? Rooms.FirstOrDefault();
                (System.Windows.Application.Current as App)?.PopoverViewModel?.UpdateRooms(groupedRooms);
            });
        }
        catch { }
    }

    private async Task ConfirmAddRoomAsync()
    {
        if (string.IsNullOrWhiteSpace(NewRoomName))
        {
            IsAddingRoom = false;
            return;
        }

        string name = NewRoomName.Trim();
        if (!AvailableRoomNames.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            AvailableRoomNames.Add(name);
            var cfg = await _configStorage.LoadConfigAsync();
            cfg.CustomRooms = AvailableRoomNames.ToList();
            await _configStorage.SaveConfigAsync(cfg);

            var allDevices = Rooms.SelectMany(r => r.Devices).DistinctBy(d => d.Mac).ToList();
            var grouped = DiscoveryService.GroupDevicesByRoom(allDevices, cfg.DeviceRoomAssignments, cfg.CustomRooms);
            RunOnUIThread(() =>
            {
                Rooms.Clear();
                foreach (var r in grouped) Rooms.Add(r);
                (System.Windows.Application.Current as App)?.PopoverViewModel?.UpdateRooms(grouped);
                foreach (var d in Devices) d.UpdateAvailableRooms(AvailableRoomNames);
            });
        }
        IsAddingRoom = false;
        NewRoomName = string.Empty;
    }

    private async Task DeleteRoomAsync(string roomName)
    {
        if (AvailableRoomNames.Count <= 1 || !AvailableRoomNames.Contains(roomName)) return;

        AvailableRoomNames.Remove(roomName);
        var cfg = await _configStorage.LoadConfigAsync();
        cfg.CustomRooms = AvailableRoomNames.ToList();

        string fallback = AvailableRoomNames[0];
        foreach (var kvp in cfg.DeviceRoomAssignments.ToList())
        {
            if (kvp.Value.Equals(roomName, StringComparison.OrdinalIgnoreCase))
            {
                cfg.DeviceRoomAssignments[kvp.Key] = fallback;
            }
        }
        await _configStorage.SaveConfigAsync(cfg);

        var allDevices = Rooms.SelectMany(r => r.Devices).DistinctBy(d => d.Mac).ToList();
        var grouped = DiscoveryService.GroupDevicesByRoom(allDevices, cfg.DeviceRoomAssignments, cfg.CustomRooms);
        RunOnUIThread(() =>
        {
            Rooms.Clear();
            foreach (var r in grouped) Rooms.Add(r);
            SelectedRoom = Rooms.FirstOrDefault();
            (System.Windows.Application.Current as App)?.PopoverViewModel?.UpdateRooms(grouped);
            foreach (var d in Devices) d.UpdateAvailableRooms(AvailableRoomNames);
        });
    }

    public void MoveDeviceUp(DeviceItemViewModel dev)
    {
        int idx = Devices.IndexOf(dev);
        if (idx > 0)
        {
            Devices.Move(idx, idx - 1);
            SaveDeviceOrder();
        }
    }

    public void MoveDeviceDown(DeviceItemViewModel dev)
    {
        int idx = Devices.IndexOf(dev);
        if (idx >= 0 && idx < Devices.Count - 1)
        {
            Devices.Move(idx, idx + 1);
            SaveDeviceOrder();
        }
    }

    public void ReorderDevice(DeviceItemViewModel source, DeviceItemViewModel target)
    {
        int oldIndex = Devices.IndexOf(source);
        int newIndex = Devices.IndexOf(target);
        if (oldIndex >= 0 && newIndex >= 0 && oldIndex != newIndex)
        {
            Devices.Move(oldIndex, newIndex);
            SaveDeviceOrder();
        }
    }

    private async void SaveDeviceOrder()
    {
        try
        {
            var cfg = await _configStorage.LoadConfigAsync();
            cfg.DeviceOrder = Devices.Select(d => d.Mac).ToList();
            await _configStorage.SaveConfigAsync(cfg);
        }
        catch { }
    }

    public void ToggleAllRoomDevices(bool power)
    {
        if (SelectedRoom == null) return;

        var pilot = new WizPilotState { State = power };
        string payload = WizProtocol.CreateSetPilotPayload(pilot);

        foreach (var devItem in Devices)
        {
            devItem.IsOn = power;
        }

        foreach (var dev in SelectedRoom.Devices)
        {
            dev.CurrentState.State = power;
            if (IPAddress.TryParse(dev.IpAddress, out var ip))
            {
                _transport.SendFireAndForget(ip, payload);
            }
        }
    }

    public void SetAllRoomBrightness(int value)
    {
        if (SelectedRoom == null) return;

        var pilot = new WizPilotState { State = true, Dimming = value };
        string payload = WizProtocol.CreateSetPilotPayload(pilot);

        foreach (var devItem in Devices)
        {
            devItem.Brightness = value;
        }

        foreach (var dev in SelectedRoom.Devices)
        {
            dev.CurrentState.State = true;
            dev.CurrentState.Dimming = value;
            if (IPAddress.TryParse(dev.IpAddress, out var ip))
            {
                _transport.SendFireAndForget(ip, payload);
            }
        }
    }

    private static void RunOnUIThread(Action action)
    {
        var dispatch = System.Windows.Application.Current?.Dispatcher;
        if (dispatch != null && !dispatch.CheckAccess())
        {
            dispatch.Invoke(action);
        }
        else
        {
            action();
        }
    }

    public async Task CaptureSceneAsync()
    {
        if (SelectedRoom == null) return;

        string name = string.IsNullOrWhiteSpace(NewSceneName) 
            ? $"Escena {DateTime.Now:HH:mm}" 
            : NewSceneName.Trim();
        string icon = string.IsNullOrWhiteSpace(NewSceneIcon) ? "💡" : NewSceneIcon.Trim();

        var scene = await _snapshotService.CaptureLiveRoomSceneAsync(SelectedRoom, name, icon);
        var vm = new SceneItemViewModel(
            scene,
            _configStorage,
            applyAction: async item => await ApplyCustomSceneAsync(item.Model),
            deleteAction: async item => await DeleteCustomSceneAsync(item),
            onChangedAction: () => OnCustomScenesChanged()
        );
        CustomScenes.Add(vm);
        NewSceneName = string.Empty;
        StatusMessage = $"Escena '{name}' capturada y guardada con éxito";
        OnCustomScenesChanged();
    }

    public async Task DeleteCustomSceneAsync(SceneItemViewModel item)
    {
        CustomScenes.Remove(item);
        try
        {
            var config = await _configStorage.LoadConfigAsync();
            config.CustomScenes.RemoveAll(s => s.Id == item.Id || s.Name == item.Name);
            await _configStorage.SaveConfigAsync(config);
            StatusMessage = $"Escena '{item.Name}' eliminada";
            OnCustomScenesChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al eliminar escena: {ex.Message}";
        }
    }

    public void OnCustomScenesChanged()
    {
        _ = RefreshQuickSlotsAsync();
        (System.Windows.Application.Current as App)?.PopoverViewModel?.RefreshQuickScenesAsync();
    }

    public async Task RefreshQuickSlotsAsync()
    {
        var config = await _configStorage.LoadConfigAsync();
        bool isEn = LocalizationService.Instance.CurrentLanguage == "en";

        var options = new List<QuickSceneOptionItem>
        {
            new() { DisplayText = isEn ? "☀️ Daylight (Preset)" : "☀️ Luz Diaria (Fábrica)", Name = isEn ? "Daylight" : "Luz Diaria", Icon = "☀️", SceneId = 12 },
            new() { DisplayText = isEn ? "🔥 Fireplace (Preset)" : "🔥 Chimenea (Fábrica)", Name = isEn ? "Fireplace" : "Chimenea", Icon = "🔥", SceneId = 5 },
            new() { DisplayText = isEn ? "🌊 Ocean (Preset)" : "🌊 Océano (Fábrica)", Name = isEn ? "Ocean" : "Océano", Icon = "🌊", SceneId = 1 },
            new() { DisplayText = isEn ? "🌅 Sunset (Preset)" : "🌅 Atardecer (Fábrica)", Name = isEn ? "Sunset" : "Atardecer", Icon = "🌅", SceneId = 3 },
            new() { DisplayText = isEn ? "🕯️ Candlelight (Preset)" : "🕯️ Vela (Fábrica)", Name = isEn ? "Candlelight" : "Vela", Icon = "🕯️", SceneId = 4 },
            new() { DisplayText = isEn ? "🌲 Forest (Preset)" : "🌲 Bosque (Fábrica)", Name = isEn ? "Forest" : "Bosque", Icon = "🌲", SceneId = 2 },
            new() { DisplayText = isEn ? "🎉 Party (Preset)" : "🎉 Fiesta (Fábrica)", Name = isEn ? "Party" : "Fiesta", Icon = "🎉", SceneId = 7 },
            new() { DisplayText = isEn ? "💖 Romance (Preset)" : "💖 Romántico (Fábrica)", Name = isEn ? "Romance" : "Romántico", Icon = "💖", SceneId = 8 },
            new() { DisplayText = isEn ? "🌙 Night Light (Preset)" : "🌙 Luz Nocturna (Fábrica)", Name = isEn ? "Night Light" : "Luz Nocturna", Icon = "🌙", SceneId = 6 },
            new() { DisplayText = "🎵 Audio Sync (WASAPI)", Name = "Audio Sync", Icon = "🎵", IsAudioSync = true }
        };

        foreach (var sc in config.CustomScenes)
        {
            options.Add(new QuickSceneOptionItem
            {
                DisplayText = $"{sc.Icon} {sc.Name} {(isEn ? "(Saved)" : "(Guardada)")}",
                Name = sc.Name,
                Icon = string.IsNullOrEmpty(sc.Icon) ? "💡" : sc.Icon,
                CustomSceneName = sc.Name
            });
        }

        var defaultConfigs = new List<QuickSceneItemConfig>
        {
            new() { Name = "Luz Diaria", Icon = "☀️", SceneId = 12 },
            new() { Name = "Chimenea", Icon = "🔥", SceneId = 5 },
            new() { Name = "Océano", Icon = "🌊", SceneId = 1 },
            new() { Name = "Atardecer", Icon = "🌅", SceneId = 3 },
            new() { Name = "Vela", Icon = "🕯️", SceneId = 4 },
            new() { Name = "Audio Sync", Icon = "🎵", IsAudioSync = true }
        };

        if (config.QuickScenes == null || config.QuickScenes.Count < 6)
        {
            config.QuickScenes = new List<QuickSceneItemConfig>(defaultConfigs);
            await _configStorage.SaveConfigAsync(config);
        }

        RunOnUIThread(() =>
        {
            if (QuickSlots.Count == 0)
            {
                for (int i = 0; i < 6; i++)
                {
                    QuickSlots.Add(new QuickSlotConfigViewModel(
                        i,
                        _configStorage,
                        () => (System.Windows.Application.Current as App)?.PopoverViewModel?.RefreshQuickScenesAsync()
                    ));
                }
            }

            for (int i = 0; i < 6; i++)
            {
                var cur = i < config.QuickScenes.Count ? config.QuickScenes[i] : null;
                QuickSlots[i].RefreshOptions(options, cur);
            }
        });
    }

    public void LoadPredefinedScenes()
    {
        PredefinedScenes.Clear();
        var scenes = new (int id, string locKey, string nameFallback, string icon, string descEs, string descEn)[]
        {
            (12, "Scene_Daylight", "Luz Diaria", "☀️", "Luz blanca natural equilibrada para productividad y lectura diaria.", "Natural balanced white light for daily productivity and reading."),
            (5, "Scene_Fireplace", "Chimenea", "🔥", "Efecto de fuego cálido con suave parpadeo dinámico en tonos ámbar.", "Warm fire effect with soft dynamic flickering in amber tones."),
            (1, "Scene_Ocean", "Océano", "🌊", "Tonos turquesa y azul profundo que recrean la calma del mar.", "Turquoise and deep blue tones recreating oceanic calm."),
            (3, "Scene_Sunset", "Atardecer", "🌅", "Gradiente crepuscular con tonos dorados, anaranjados y rojizos.", "Twilight gradient with golden, orange and reddish tones."),
            (4, "Scene_Candlelight", "Vela", "🕯️", "Iluminación íntima y tenue con simulación de llama parpadeante.", "Intimate and gentle lighting simulating candle flicker."),
            (2, "Scene_Forest", "Bosque", "🌲", "Tonos verdes y esmeralda de naturaleza fresca y relajante.", "Green and emerald tones of fresh, relaxing nature."),
            (7, "Scene_Party", "Fiesta", "🎉", "Ciclo dinámico y festivo con cambios vibrantes multicolor.", "Dynamic party cycle with vibrant multicolor changes."),
            (8, "Scene_Romance", "Romance", "💖", "Ambiente envolvente en matices suaves de rosa, lila y magenta.", "Enveloping ambiance in soft pink, lilac and magenta shades."),
            (6, "Scene_NightLight", "Luz Nocturna", "🌙", "Mínimo nivel de brillo para descansar sin deslumbrar.", "Lowest brightness level for resting without glare."),
            (999, "Scene_AudioSync", "Audio Sync", "🎵", "Efecto reactivo en tiempo real al sonido y música de tu PC.", "Real-time reactive effect to your PC sound and music.")
        };

        bool isEn = LocalizationService.Instance.CurrentLanguage == "en";
        foreach (var s in scenes)
        {
            string name = LocalizationService.Tr(s.locKey);
            if (name == s.locKey) name = s.nameFallback;
            string desc = isEn ? s.descEn : s.descEs;

            PredefinedScenes.Add(new PredefinedSceneViewModel(
                s.id,
                name,
                s.icon,
                desc,
                s.id == 999,
                isPinned: true,
                applyAction: async item =>
                {
                    if (item.IsAudioSync)
                    {
                        ToggleAudioSync();
                    }
                    else
                    {
                        await ApplyPredefinedSceneAsync(item.SceneId, item.Name);
                    }
                },
                togglePinAction: item =>
                {
                    (System.Windows.Application.Current as App)?.PopoverViewModel?.RefreshQuickScenesAsync();
                }
            ));
        }
    }

    public Task ApplyPredefinedSceneAsync(int sceneId, string sceneName)
    {
        if (SelectedRoom == null) return Task.CompletedTask;

        if (_isAudioSyncActive)
        {
            _audioSyncService.Stop();
            IsAudioSyncActive = false;
        }

        var pilot = new WizPilotState { State = true, SceneId = sceneId };
        string payload = WizProtocol.CreateSetPilotPayload(pilot);

        foreach (var dev in SelectedRoom.Devices)
        {
            dev.CurrentState.State = true;
            dev.CurrentState.SceneId = sceneId;
            if (IPAddress.TryParse(dev.IpAddress, out var ip))
            {
                _transport.SendFireAndForget(ip, payload);
            }
        }

        foreach (var devVm in Devices)
        {
            devVm.IsOn = true;
        }

        StatusMessage = $"Escena predefinida '{sceneName}' aplicada a {SelectedRoom.Name}";
        return Task.CompletedTask;
    }

    public async Task ApplyCustomSceneAsync(WizCustomScene scene)
    {
        if (SelectedRoom == null) return;

        await _snapshotService.ApplySceneAsync(scene, SelectedRoom.Devices);
        PopulateDevicesForSelectedRoom();
        StatusMessage = $"Escena '{scene.Name}' aplicada a {SelectedRoom.Name}";
    }

    public void RefreshAudioDevices()
    {
        RunOnUIThread(() =>
        {
            AudioDevices.Clear();
            var devs = AudioSyncService.GetAvailableAudioDevices();
            foreach (var d in devs)
            {
                AudioDevices.Add(d);
            }
            SelectedAudioDevice = AudioDevices.FirstOrDefault(d => d.IsDefault) ?? AudioDevices.FirstOrDefault();
        });
    }

    public void ToggleAudioSync()
    {
        if (SelectedRoom == null) return;

        if (_audioSyncService.IsRunning)
        {
            _audioSyncService.Stop();
            IsAudioSyncActive = false;
            StatusMessage = "Audio Reactivo detenido";
        }
        else
        {
            _audioSyncService.Start(SelectedRoom.Devices, SelectedAudioDevice?.Id);
            IsAudioSyncActive = true;
            StatusMessage = $"Audio Reactivo digital (WASAPI) activo en: {SelectedAudioDevice?.Name ?? "Predeterminado"}";
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? prop = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
