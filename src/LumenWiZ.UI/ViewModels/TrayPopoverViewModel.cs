using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using LumenWiZ.Core.Audio;
using LumenWiZ.Core.Models;
using LumenWiZ.Core.Network;
using LumenWiZ.Core.Protocol;
using LumenWiZ.Core.Services;
using LumenWiZ.Core.Storage;
using LumenWiZ.UI.Common;

namespace LumenWiZ.UI.ViewModels;

public class TrayPopoverViewModel : INotifyPropertyChanged
{
    private readonly WizUdpTransport _transport;
    private readonly DiscoveryService _discoveryService;
    private readonly SceneSnapshotService _snapshotService;
    private readonly AudioSyncService _audioSyncService;
    private readonly ConfigStorage? _configStorage;

    private WizRoom? _activeRoom;
    private int _brightness = 80;
    private bool _isMasterOn = true;
    private string _activeSceneName = "Luz Diaria";
    private bool _isAudioSyncActive;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? OpenSettingsRequested;

    public ObservableCollection<QuickSceneItemViewModel> PredefinedQuickScenes { get; } = new();
    public ObservableCollection<QuickSceneItemViewModel> CustomQuickScenes { get; } = new();
    public ObservableCollection<QuickSceneItemViewModel> QuickScenes { get; } = new();
    public ObservableCollection<QuickSlotConfigViewModel> QuickSlots { get; } = new();

    private bool _isEditingSlots;
    public bool IsEditingSlots
    {
        get => _isEditingSlots;
        set { _isEditingSlots = value; OnPropertyChanged(); }
    }

    private bool _hasCustomQuickScenes;
    public bool HasCustomQuickScenes
    {
        get => _hasCustomQuickScenes;
        set { _hasCustomQuickScenes = value; OnPropertyChanged(); }
    }

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
            if (ActiveRoom == null) return LocalizationService.Instance.CurrentLanguage == "en" ? "Looking for lights..." : "Buscando luces...";
            int count = ActiveRoom.Devices.Count;
            return LocalizationService.TrFormat("Loc_ActiveLights", count);
        }
    }

    public ObservableCollection<WizRoom> AvailableRooms { get; } = new();

    public System.Windows.Input.ICommand OpenSettingsCommand { get; }
    public System.Windows.Input.ICommand ToggleAudioSyncCommand { get; }
    public System.Windows.Input.ICommand SetSceneCommand { get; }
    public System.Windows.Input.ICommand TogglePowerCommand { get; }
    public System.Windows.Input.ICommand ToggleEditSlotsCommand { get; }
    public System.Windows.Input.ICommand OpenFullStudioCommand { get; }

    public TrayPopoverViewModel(
        WizUdpTransport transport, 
        DiscoveryService discoveryService, 
        SceneSnapshotService snapshotService,
        AudioSyncService audioSyncService,
        ConfigStorage? configStorage = null)
    {
        _transport = transport;
        _discoveryService = discoveryService;
        _snapshotService = snapshotService;
        _audioSyncService = audioSyncService;
        _configStorage = configStorage;

        OpenSettingsCommand = new LumenWiZ.UI.Common.RelayCommand(() => RequestOpenSettings());
        ToggleAudioSyncCommand = new LumenWiZ.UI.Common.RelayCommand(() => ToggleAudioSync());
        SetSceneCommand = new LumenWiZ.UI.Common.RelayCommand(param =>
        {
            if (param is string sceneStr)
            {
                var parts = sceneStr.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out int scId))
                {
                    SetScene(parts[0], scId);
                }
            }
        });
        TogglePowerCommand = new LumenWiZ.UI.Common.RelayCommand(() => IsMasterOn = !IsMasterOn);
        ToggleEditSlotsCommand = new LumenWiZ.UI.Common.RelayCommand(async () => await ToggleEditSlotsAsync());
        OpenFullStudioCommand = new LumenWiZ.UI.Common.RelayCommand(() => RequestOpenSettings());

        LocalizationService.Instance.LanguageChanged += () =>
        {
            OnPropertyChanged(nameof(RoomStatusText));
            _ = RefreshQuickScenesAsync();
        };

        _ = RefreshQuickScenesAsync();
    }

    public async Task ToggleEditSlotsAsync()
    {
        IsEditingSlots = !IsEditingSlots;
        if (IsEditingSlots && _configStorage != null)
        {
            try
            {
                var cfg = await _configStorage.LoadConfigAsync();
                var options = BuildQuickSceneOptions(cfg);

                if (QuickSlots.Count == 0)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        QuickSlots.Add(new QuickSlotConfigViewModel(
                            i,
                            _configStorage,
                            () => _ = RefreshQuickScenesAsync()
                        ));
                    }
                }

                for (int i = 0; i < 6; i++)
                {
                    var cur = i < cfg.QuickScenes.Count ? cfg.QuickScenes[i] : null;
                    QuickSlots[i].RefreshOptions(options, cur);
                }
            }
            catch { }
        }
    }

    public static List<QuickSceneOptionItem> BuildQuickSceneOptions(AppConfig config)
    {
        bool isEn = LocalizationService.Instance.CurrentLanguage == "en";
        var options = new List<QuickSceneOptionItem>
        {
            new() { DisplayText = isEn ? "☀️ Daylight" : "☀️ Luz Diaria", Name = isEn ? "Daylight" : "Luz Diaria", Icon = "☀️", SceneId = 12 },
            new() { DisplayText = isEn ? "🔥 Fireplace" : "🔥 Chimenea", Name = isEn ? "Fireplace" : "Chimenea", Icon = "🔥", SceneId = 5 },
            new() { DisplayText = isEn ? "🌊 Ocean" : "🌊 Océano", Name = isEn ? "Ocean" : "Océano", Icon = "🌊", SceneId = 1 },
            new() { DisplayText = isEn ? "🌅 Sunset" : "🌅 Atardecer", Name = isEn ? "Sunset" : "Atardecer", Icon = "🌅", SceneId = 3 },
            new() { DisplayText = isEn ? "🕯️ Candlelight" : "🕯️ Vela", Name = isEn ? "Candlelight" : "Vela", Icon = "🕯️", SceneId = 4 },
            new() { DisplayText = isEn ? "🌲 Forest" : "🌲 Bosque", Name = isEn ? "Forest" : "Bosque", Icon = "🌲", SceneId = 2 },
            new() { DisplayText = isEn ? "🎉 Party" : "🎉 Fiesta", Name = isEn ? "Party" : "Fiesta", Icon = "🎉", SceneId = 7 },
            new() { DisplayText = isEn ? "💖 Romance" : "💖 Romántico", Name = isEn ? "Romance" : "Romántico", Icon = "💖", SceneId = 8 },
            new() { DisplayText = isEn ? "🌙 Night Light" : "🌙 Luz Nocturna", Name = isEn ? "Night Light" : "Luz Nocturna", Icon = "🌙", SceneId = 6 },
            new() { DisplayText = "🎵 Audio Sync", Name = "Audio Sync", Icon = "🎵", IsAudioSync = true }
        };

        if (config.CustomScenes != null)
        {
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
        }

        return options;
    }

    public async Task RefreshStateAsync()
    {
        if (ActiveRoom != null && ActiveRoom.Devices.Count > 0)
        {
            var pingTasks = ActiveRoom.Devices.Select(async d =>
            {
                if (IPAddress.TryParse(d.IpAddress, out var ip))
                {
                    string? pilotJson = await _transport.SendAsync(ip, WizProtocol.CreateGetPilotPayload(), 400);
                    if (!string.IsNullOrEmpty(pilotJson))
                    {
                        d.CurrentState = WizProtocol.ParsePilotResponse(pilotJson);
                        d.IsOnline = true;
                    }
                }
            });
            await Task.WhenAll(pingTasks);

            _isMasterOn = ActiveRoom.Devices.Any(d => d.CurrentState.State);
            var onDevs = ActiveRoom.Devices.Where(d => d.CurrentState.State).ToList();
            if (onDevs.Count > 0)
            {
                _brightness = (int)onDevs.Average(d => d.CurrentState.Dimming ?? 80);
                int? sceneId = onDevs.FirstOrDefault()?.CurrentState.SceneId;
                if (sceneId.HasValue && sceneId.Value > 0)
                {
                    _activeSceneName = GetSceneNameById(sceneId.Value);
                    OnPropertyChanged(nameof(ActiveSceneName));
                }
            }

            OnPropertyChanged(nameof(IsMasterOn));
            OnPropertyChanged(nameof(Brightness));
            OnPropertyChanged(nameof(RoomStatusText));
            return;
        }

        var cfg = _configStorage != null ? await _configStorage.LoadConfigAsync() : new AppConfig();
        var devices = await _discoveryService.DiscoverDevicesAsync(1800, cfg.DeviceNames, cfg.DeviceAudioBands);
        var rooms = DiscoveryService.GroupDevicesByRoom(devices, cfg.DeviceRoomAssignments, cfg.CustomRooms);

        var dispatch = System.Windows.Application.Current?.Dispatcher;
        if (dispatch != null && !dispatch.CheckAccess())
        {
            dispatch.Invoke(() => UpdateRooms(rooms));
        }
        else
        {
            UpdateRooms(rooms);
        }
    }

    public static string GetSceneNameById(int sceneId) => sceneId switch
    {
        1 => "Océano",
        2 => "Romance",
        3 => "Atardecer",
        4 => "Vela",
        5 => "Chimenea",
        6 => "Acogedor",
        7 => "Bosque",
        8 => "Pastel",
        9 => "Blanco y Negro",
        10 => "Primavera",
        11 => "Verano",
        12 => "Luz Diaria",
        13 => "Luz de Noche",
        14 => "Enfoque",
        15 => "Relax",
        16 => "Blanco Cálido",
        17 => "Blanco Frío",
        _ => "Escena Activa"
    };

    public void UpdateRooms(List<WizRoom> rooms)
    {
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

    public void SetScene(string sceneName, int sceneId)
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
            device.CurrentState.State = true;
            device.CurrentState.SceneId = sceneId;
            if (IPAddress.TryParse(device.IpAddress, out var ip))
            {
                _transport.SendFireAndForget(ip, payload);
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

    private void ApplyRoomBrightnessAsync(int value)
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
            device.CurrentState.State = true;
            device.CurrentState.Dimming = value;
            if (IPAddress.TryParse(device.IpAddress, out var ip))
            {
                _transport.SendFireAndForget(ip, payload);
            }
        }
    }

    private void ToggleRoomPowerAsync(bool power)
    {
        if (ActiveRoom == null) return;

        var pilotState = new WizPilotState { State = power };
        string payload = WizProtocol.CreateSetPilotPayload(pilotState);

        foreach (var device in ActiveRoom.Devices)
        {
            device.CurrentState.State = power;
            if (IPAddress.TryParse(device.IpAddress, out var ip))
            {
                _transport.SendFireAndForget(ip, payload);
            }
        }
        OnPropertyChanged(nameof(RoomStatusText));
    }

    public void RequestOpenSettings()
    {
        string logDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LumenWiZ");
        string logFile = System.IO.Path.Combine(logDir, "debug.log");
        System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] TrayPopoverViewModel: RequestOpenSettings called\n");
        OpenSettingsRequested?.Invoke();
    }

    public async Task RefreshQuickScenesAsync()
    {
        var defaultList = new List<QuickSceneItemConfig>
        {
            new() { Name = "Luz Diaria", Icon = "☀️", SceneId = 12 },
            new() { Name = "Chimenea", Icon = "🔥", SceneId = 5 },
            new() { Name = "Océano", Icon = "🌊", SceneId = 1 },
            new() { Name = "Atardecer", Icon = "🌅", SceneId = 3 },
            new() { Name = "Vela", Icon = "🕯️", SceneId = 4 },
            new() { Name = "Audio Sync", Icon = "🎵", IsAudioSync = true }
        };

        List<QuickSceneItemConfig> activeList = defaultList;
        if (_configStorage != null)
        {
            try
            {
                var cfg = await _configStorage.LoadConfigAsync();
                if (cfg.QuickScenes != null && cfg.QuickScenes.Count > 0)
                {
                    activeList = cfg.QuickScenes;
                }
                else
                {
                    cfg.QuickScenes = new List<QuickSceneItemConfig>(defaultList);
                    await _configStorage.SaveConfigAsync(cfg);
                    activeList = cfg.QuickScenes;
                }
            }
            catch { }
        }

        var dispatch = System.Windows.Application.Current?.Dispatcher;
        Action updateAction = () =>
        {
            QuickScenes.Clear();
            foreach (var itm in activeList)
            {
                QuickScenes.Add(new QuickSceneItemViewModel(itm, OnActivateQuickScene));
            }
        };

        if (dispatch != null && !dispatch.CheckAccess())
            dispatch.Invoke(updateAction);
        else
            updateAction();
    }

    private async void OnActivateQuickScene(QuickSceneItemViewModel item)
    {
        if (item.IsAudioSync)
        {
            ToggleAudioSync();
            return;
        }

        if (!string.IsNullOrEmpty(item.CustomSceneName) && _configStorage != null && ActiveRoom != null)
        {
            var cfg = await _configStorage.LoadConfigAsync();
            var saved = cfg.CustomScenes.FirstOrDefault(s => s.Name.Equals(item.CustomSceneName, StringComparison.OrdinalIgnoreCase));
            if (saved != null)
            {
                if (_audioSyncService.IsRunning)
                {
                    _audioSyncService.Stop();
                    IsAudioSyncActive = false;
                }
                await _snapshotService.ApplySceneAsync(saved, ActiveRoom.Devices);
                ActiveSceneName = saved.Name;
                return;
            }
        }

        if (item.SceneId.HasValue)
        {
            SetScene(item.Name, item.SceneId.Value);
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? prop = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
