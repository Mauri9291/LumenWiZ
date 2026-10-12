using System.Windows;
using System.Windows.Input;
using LumenWiZ.UI.ViewModels;

namespace LumenWiZ.UI.Views;

public partial class TrayPopoverWindow : Window
{
    private readonly TrayPopoverViewModel _viewModel;

    public TrayPopoverWindow(TrayPopoverViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    public void ShowAtTray()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 14;
        double h = ActualHeight > 0 ? ActualHeight : 280;
        Top = workArea.Bottom - h - 14;

        Show();
        UpdateLayout();
        Top = workArea.Bottom - ActualHeight - 14;
        Activate();
        // UI is pre-loaded and cached. Do NOT call RefreshStateAsync on open to prevent lag and reconstruction!
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (IsVisible && ActualHeight > 0)
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 14;
            Top = workArea.Bottom - ActualHeight - 14;
        }
    }

    public void ToggleVisibility()
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            ShowAtTray();
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (GetCursorPos(out POINT pt))
        {
            double h = ActualHeight > 0 ? ActualHeight : 300;
            var winRect = new Rect(Left - 10, Top - 10, Width + 20, h + 250);
            if (winRect.Contains(new Point(pt.X, pt.Y)))
            {
                return;
            }
        }
        Hide();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        OpenSettings();
    }

    private void OpenSettings()
    {
        string logDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LumenWiZ");
        string logFile = System.IO.Path.Combine(logDir, "debug.log");
        System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] TrayPopover: OpenSettings invoked -> Nav 0\n");

        Hide();
        if (Application.Current is App app)
        {
            app.ShowMainWindow(0); // Nav 0: Mis Luces / Studio Completo
        }
    }

    private void EditScenesButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        OpenSceneEditor();
    }

    private void EditScenesButton_Click(object sender, RoutedEventArgs e)
    {
        OpenSceneEditor();
    }

    private void OpenSceneEditor()
    {
        _viewModel.ToggleEditSlotsCommand.Execute(null);
    }

    public void OpenFullStudio(int nav = 2, int sub = 2)
    {
        Hide();
        if (Application.Current is App app)
        {
            app.ShowMainWindow(nav, sub);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Hide();
        }
    }
}
