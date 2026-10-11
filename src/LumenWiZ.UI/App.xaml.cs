using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using LumenWiZ.Core.Audio;
using LumenWiZ.Core.Network;
using LumenWiZ.Core.Server;
using LumenWiZ.Core.Services;
using LumenWiZ.Core.Storage;
using LumenWiZ.UI.ViewModels;
using LumenWiZ.UI.Views;

namespace LumenWiZ.UI;

public partial class App : Application
{
    private WizUdpTransport? _transport;
    private DiscoveryService? _discoveryService;
    private ConfigStorage? _configStorage;
    private SceneSnapshotService? _snapshotService;
    private AudioSyncService? _audioSyncService;
    private LocalHttpServer? _httpServer;

    private TaskbarIcon? _trayIcon;
    private TrayPopoverWindow? _popoverWindow;
    private TrayPopoverViewModel? _popoverViewModel;
    private MainWindow? _mainWindow;

    public WizUdpTransport? Transport => _transport;
    public DiscoveryService? DiscoveryService => _discoveryService;
    public ConfigStorage? ConfigStorage => _configStorage;
    public SceneSnapshotService? SnapshotService => _snapshotService;
    public AudioSyncService? AudioSyncService => _audioSyncService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Initialize Core Services
        _transport = new WizUdpTransport();
        _discoveryService = new DiscoveryService(_transport);
        _configStorage = new ConfigStorage();
        _snapshotService = new SceneSnapshotService(_transport, _configStorage);
        _audioSyncService = new AudioSyncService(_transport);

        // 2. Start Embedded Local HTTP Server for Stream Deck
        _httpServer = new LocalHttpServer(38898);
        _httpServer.GetRoomsHandler = () => _popoverViewModel?.AvailableRooms ?? Enumerable.Empty<LumenWiZ.Core.Models.WizRoom>();
        _httpServer.ToggleRoomPowerHandler = async (roomId, power) =>
        {
            var room = _popoverViewModel?.AvailableRooms.FirstOrDefault(r => r.RoomId == roomId);
            if (room != null && _transport != null)
            {
                var pilot = new LumenWiZ.Core.Models.WizPilotState { State = power };
                string payload = LumenWiZ.Core.Protocol.WizProtocol.CreateSetPilotPayload(pilot);
                foreach (var dev in room.Devices)
                {
                    if (System.Net.IPAddress.TryParse(dev.IpAddress, out var ip))
                        await _transport.SendAsync(ip, payload, 30);
                }
            }
        };
        _httpServer.ActivateSceneHandler = async (sceneName) =>
        {
            if (_snapshotService != null && _configStorage != null && _popoverViewModel?.ActiveRoom != null)
            {
                var config = await _configStorage.LoadConfigAsync();
                var scene = config.CustomScenes.FirstOrDefault(s => s.Name.Equals(sceneName, StringComparison.OrdinalIgnoreCase));
                if (scene != null)
                {
                    await _snapshotService.ApplySceneAsync(scene, _popoverViewModel.ActiveRoom.Devices);
                }
            }
        };
        _httpServer.SetRoomBrightnessHandler = async (roomId, brightness) =>
        {
            var room = _popoverViewModel?.AvailableRooms.FirstOrDefault(r => r.RoomId == roomId);
            if (room != null && _transport != null)
            {
                var pilot = new LumenWiZ.Core.Models.WizPilotState { State = true, Dimming = brightness };
                string payload = LumenWiZ.Core.Protocol.WizProtocol.CreateSetPilotPayload(pilot);
                foreach (var dev in room.Devices)
                {
                    if (System.Net.IPAddress.TryParse(dev.IpAddress, out var ip))
                        await _transport.SendAsync(ip, payload, 30);
                }
            }
        };

        try
        {
            _httpServer.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to start local HTTP server: {ex.Message}");
        }

        // 3. Initialize Tray Popover
        _popoverViewModel = new TrayPopoverViewModel(
            _transport, 
            _discoveryService, 
            _snapshotService, 
            _audioSyncService);
        _popoverViewModel.OpenSettingsRequested += ShowMainWindow;

        _popoverWindow = new TrayPopoverWindow(_popoverViewModel);

        // 4. Initialize System Tray Icon
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "LumenWiZ - WiZ Lights Studio",
            Icon = CreateBulbIcon()
        };

        _trayIcon.TrayLeftMouseDown += (s, args) =>
        {
            _popoverWindow.ToggleVisibility();
        };

        _trayIcon.TrayMouseDoubleClick += (s, args) =>
        {
            ShowMainWindow();
        };

        // Tray Context Menu
        var contextMenu = new ContextMenu();
        var itemOpenWindow = new MenuItem { Header = "💡 Abrir WiZ Lights Studio" };
        itemOpenWindow.Click += (s, args) => ShowMainWindow();
        var itemOpenQuick = new MenuItem { Header = "⚡ Control Rápido" };
        itemOpenQuick.Click += (s, args) => _popoverWindow.ShowAtTray();
        var itemExit = new MenuItem { Header = "❌ Salir" };
        itemExit.Click += (s, args) => ShutdownApp();

        contextMenu.Items.Add(itemOpenWindow);
        contextMenu.Items.Add(itemOpenQuick);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(itemExit);

        _trayIcon.ContextMenu = contextMenu;

        // Perform initial discovery in background
        _ = _popoverViewModel.RefreshStateAsync();
    }

    public void ShowMainWindow()
    {
        if (_mainWindow == null || !_mainWindow.IsLoaded)
        {
            _mainWindow = new MainWindow();
        }
        _mainWindow.Show();
        _mainWindow.Activate();
    }

    public void ShutdownApp()
    {
        _trayIcon?.Dispose();
        _audioSyncService?.Dispose();
        _httpServer?.Stop();
        _transport?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ShutdownApp();
        base.OnExit(e);
    }

    private static Icon CreateBulbIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(System.Drawing.Color.Transparent);
        using var brush = new SolidBrush(System.Drawing.Color.FromArgb(245, 158, 11));
        g.FillEllipse(brush, 6, 4, 20, 20);
        using var baseBrush = new SolidBrush(System.Drawing.Color.FromArgb(180, 180, 180));
        g.FillRectangle(baseBrush, 11, 22, 10, 6);
        return Icon.FromHandle(bmp.GetHicon());
    }
}
