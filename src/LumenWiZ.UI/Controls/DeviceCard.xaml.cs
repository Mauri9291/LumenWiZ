using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LumenWiZ.UI.ViewModels;

namespace LumenWiZ.UI.Controls;

public partial class DeviceCard : UserControl
{
    private Point _dragStartPoint;

    public DeviceCard()
    {
        InitializeComponent();
        AllowDrop = true;

        PreviewMouseLeftButtonDown += (s, e) =>
        {
            _dragStartPoint = e.GetPosition(this);
        };

        PreviewMouseMove += (s, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPoint = e.GetPosition(this);
                var diff = _dragStartPoint - currentPoint;

                if ((Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                     Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance) &&
                    e.OriginalSource is not TextBox &&
                    e.OriginalSource is not Slider &&
                    e.OriginalSource is not Button)
                {
                    if (DataContext is DeviceItemViewModel dev)
                    {
                        DragDrop.DoDragDrop(this, dev, DragDropEffects.Move);
                    }
                }
            }
        };

        Drop += (s, e) =>
        {
            if (e.Data.GetData(typeof(DeviceItemViewModel)) is DeviceItemViewModel sourceDev &&
                DataContext is DeviceItemViewModel targetDev &&
                sourceDev != targetDev)
            {
                var win = Window.GetWindow(this);
                if (win?.DataContext is MainViewModel mainVm)
                {
                    mainVm.ReorderDevice(sourceDev, targetDev);
                }
            }
        };
    }
}
