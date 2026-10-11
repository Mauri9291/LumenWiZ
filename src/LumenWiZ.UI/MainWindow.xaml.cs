using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using LumenWiZ.UI.ViewModels;

namespace LumenWiZ.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();

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
            Loaded += async (s, e) => await _viewModel.InitializeAsync();
        }
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Don't terminate app on window close; minimize/hide to tray
        e.Cancel = true;
        Hide();
    }
}