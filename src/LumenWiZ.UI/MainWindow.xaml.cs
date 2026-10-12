using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using LumenWiZ.UI.Common;
using LumenWiZ.UI.ViewModels;

namespace LumenWiZ.UI;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly MainViewModel? _viewModel;

    public MainWindow()
    {
        string logDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LumenWiZ");
        string logFile = System.IO.Path.Combine(logDir, "debug.log");
        System.IO.File.AppendAllText(logFile, "MainWindow: Constructor enter\n");

        InitializeComponent();
        System.IO.File.AppendAllText(logFile, "MainWindow: InitializeComponent done\n");

        var app = (App)Application.Current;
        if (app.DiscoveryService != null && 
            app.SnapshotService != null && 
            app.AudioSyncService != null && 
            app.Transport != null && 
            app.ConfigStorage != null)
        {
            _viewModel = new MainViewModel(
                app.DiscoveryService,
                app.SnapshotService,
                app.AudioSyncService,
                app.Transport,
                app.ConfigStorage);

            DataContext = _viewModel;
            Loaded += async (s, e) =>
            {
                System.IO.File.AppendAllText(logFile, "MainWindow: Loaded event fired\n");
                try
                {
                    await _viewModel.InitializeAsync();
                    System.IO.File.AppendAllText(logFile, "MainWindow: InitializeAsync done successfully\n");
                }
                catch (Exception ex)
                {
                    System.IO.File.AppendAllText(logFile, $"MainWindow: InitializeAsync ERROR: {ex}\n");
                }
            };
        }
        System.IO.File.AppendAllText(logFile, "MainWindow: Constructor exit\n");
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Don't terminate app on window close; minimize/hide to tray
        e.Cancel = true;
        Hide();
    }
}