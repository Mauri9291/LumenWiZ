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
        // Position at bottom-right corner with 12px margin
        Left = workArea.Right - Width - 12;
        Top = workArea.Bottom - Height - 12;

        Show();
        Activate();
        _ = _viewModel.RefreshStateAsync();
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

    private void Window_Deactivated(object sender, EventArgs e)
    {
        Hide();
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
