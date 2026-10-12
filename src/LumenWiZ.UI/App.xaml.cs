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
using LumenWiZ.UI.Common;
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
    private MainWindow? _studioWindow;

    public WizUdpTransport? Transport => _transport;
    public DiscoveryService? DiscoveryService => _discoveryService;
    public ConfigStorage? ConfigStorage => _configStorage;
    public SceneSnapshotService? SnapshotService => _snapshotService;
    public AudioSyncService? AudioSyncService => _audioSyncService;
    public TrayPopoverViewModel? PopoverViewModel => _popoverViewModel;
    public MainWindow? StudioWindow => _studioWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string logDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LumenWiZ");
        System.IO.Directory.CreateDirectory(logDir);
        string logFile = System.IO.Path.Combine(logDir, "debug.log");
        System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] LumenWiZ starting up...\n");

        DispatcherUnhandledException += (s, args) =>
        {
            System.IO.File.AppendAllText(logFile, $"[Dispatcher ERROR] {args.Exception}\n");
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            System.IO.File.AppendAllText(logFile, $"[AppDomain ERROR] {args.ExceptionObject}\n");
        };

        System.IO.File.AppendAllText(logFile, "Step 1: Initializing core services...\n");
        // 1. Initialize Core Services
        _transport = new WizUdpTransport();
        _discoveryService = new DiscoveryService(_transport);
        _configStorage = new ConfigStorage();
        var initialConfig = _configStorage.LoadConfig();
        LocalizationService.Instance.CurrentLanguage = initialConfig.Language ?? "es";
        LocalizationService.Instance.ApplyToApplicationResources();
        WindowsStartupHelper.ApplyStartupConfig(initialConfig.StartWithWindows, initialConfig.StartMinimized);

        _snapshotService = new SceneSnapshotService(_transport, _configStorage);
        _audioSyncService = new AudioSyncService(_transport);

        System.IO.File.AppendAllText(logFile, "Step 2: Starting HTTP server...\n");
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
            System.IO.File.AppendAllText(logFile, "Step 2b: HTTP server started successfully\n");
        }
        catch (Exception ex)
        {
            System.IO.File.AppendAllText(logFile, $"Step 2b: HTTP server exception: {ex.Message}\n");
        }

        System.IO.File.AppendAllText(logFile, "Step 3: Initializing Main Studio Window...\n");
        // 3. Initialize Main Studio Window FIRST so MainWindow is definitely _studioWindow
        _studioWindow = new MainWindow();
        MainWindow = _studioWindow;

        System.IO.File.AppendAllText(logFile, "Step 4: Initializing Tray Popover...\n");
        // 4. Initialize Tray Popover
        _popoverViewModel = new TrayPopoverViewModel(
            _transport, 
            _discoveryService, 
            _snapshotService, 
            _audioSyncService,
            _configStorage);
        _popoverViewModel.OpenSettingsRequested += () => ShowMainWindow(0);

        _popoverWindow = new TrayPopoverWindow(_popoverViewModel);

        System.IO.File.AppendAllText(logFile, "Step 5: Initializing System Tray Icon...\n");
        // 5. Initialize System Tray Icon
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
            ShowMainWindow(0);
        };

        // Tray Context Menu
        var contextMenu = new ContextMenu();
        var itemOpenWindow = new MenuItem { Header = LocalizationService.Tr("Loc_Tray_OpenStudio") };
        itemOpenWindow.Click += (s, args) => ShowMainWindow(0);
        var itemOpenQuick = new MenuItem { Header = LocalizationService.Tr("Loc_Tray_QuickControl") };
        itemOpenQuick.Click += (s, args) => _popoverWindow.ShowAtTray();
        var itemExit = new MenuItem { Header = LocalizationService.Tr("Loc_Tray_Exit") };
        itemExit.Click += (s, args) => ShutdownApp();

        contextMenu.Items.Add(itemOpenWindow);
        contextMenu.Items.Add(itemOpenQuick);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(itemExit);

        LocalizationService.Instance.LanguageChanged += () =>
        {
            Dispatcher.Invoke(() =>
            {
                itemOpenWindow.Header = LocalizationService.Tr("Loc_Tray_OpenStudio");
                itemOpenQuick.Header = LocalizationService.Tr("Loc_Tray_QuickControl");
                itemExit.Header = LocalizationService.Tr("Loc_Tray_Exit");
            });
        };

        _trayIcon.ContextMenu = contextMenu;

        bool startMinimized = e.Args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
        if (startMinimized)
        {
            System.IO.File.AppendAllText(logFile, "Step 6: Started with --minimized. Keeping studio window in tray.\n");
            _studioWindow.WindowState = WindowState.Minimized;
            _studioWindow.Hide();
        }
        else
        {
            System.IO.File.AppendAllText(logFile, "Step 6: Showing Main Studio Window...\n");
            _studioWindow.Show();
        }
    }

    public void ShowMainWindow() => ShowMainWindow(0, -1);

    public void ShowMainWindow(int targetNavIndex, int targetSubTabIndex = -1)
    {
        string logDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LumenWiZ");
        string logFile = System.IO.Path.Combine(logDir, "debug.log");
        System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] App.ShowMainWindow entered, targetNavIndex={targetNavIndex}, targetSubTabIndex={targetSubTabIndex}\n");

        Dispatcher.Invoke(() =>
        {
            try
            {
                _popoverWindow?.Hide();
                if (_studioWindow == null)
                {
                    System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] App.ShowMainWindow: creating new _studioWindow\n");
                    _studioWindow = new MainWindow();
                    MainWindow = _studioWindow;
                }

                if (_studioWindow.DataContext is MainViewModel vm)
                {
                    if (targetNavIndex >= 0)
                    {
                        vm.SelectedNavIndex = targetNavIndex;
                        System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] App.ShowMainWindow: switched SelectedNavIndex to {targetNavIndex}\n");
                    }
                    if (targetSubTabIndex >= 0)
                    {
                        vm.SelectedScenesTabIndex = targetSubTabIndex;
                        System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] App.ShowMainWindow: switched SelectedScenesTabIndex to {targetSubTabIndex}\n");
                    }
                }

                System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] App.ShowMainWindow: showing _studioWindow, current visibility={_studioWindow.Visibility}, state={_studioWindow.WindowState}\n");
                if (_studioWindow.WindowState == WindowState.Minimized)
                {
                    _studioWindow.WindowState = WindowState.Normal;
                }

                _studioWindow.Visibility = Visibility.Visible;
                _studioWindow.Show();

                // Win32 forced foreground activation for Windows 11
                var helper = new System.Windows.Interop.WindowInteropHelper(_studioWindow);
                IntPtr hwnd = helper.Handle;
                if (hwnd != IntPtr.Zero)
                {
                    ShowWindow(hwnd, SW_RESTORE);
                    ShowWindow(hwnd, SW_SHOW);
                    BringWindowToTop(hwnd);
                    SetForegroundWindow(hwnd);
                }

                _studioWindow.Activate();
                _studioWindow.Focus();
                System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] App.ShowMainWindow: _studioWindow activated successfully\n");
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] App.ShowMainWindow ERROR: {ex}\n");
            }
        });
    }

    public void ShutdownApp()
    {
        string logDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LumenWiZ");
        string logFile = System.IO.Path.Combine(logDir, "debug.log");
        System.IO.File.AppendAllText(logFile, $"ShutdownApp() called from:\n{Environment.StackTrace}\n");

        _trayIcon?.Dispose();
        _audioSyncService?.Dispose();
        _httpServer?.Stop();
        _transport?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        string logDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LumenWiZ");
        string logFile = System.IO.Path.Combine(logDir, "debug.log");
        System.IO.File.AppendAllText(logFile, $"OnExit() called with code {e.ApplicationExitCode} from:\n{Environment.StackTrace}\n");

        ShutdownApp();
        base.OnExit(e);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    private static Icon CreateBulbIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.Transparent);
            using var brush = new SolidBrush(System.Drawing.Color.FromArgb(245, 158, 11));
            g.FillEllipse(brush, 6, 4, 20, 20);
            using var baseBrush = new SolidBrush(System.Drawing.Color.FromArgb(180, 180, 180));
            g.FillRectangle(baseBrush, 11, 22, 10, 6);
        }

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var tempIcon = Icon.FromHandle(hIcon);
            return (Icon)tempIcon.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }
}
