using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LumenWiZ.UI.ViewModels;

namespace LumenWiZ.UI.Controls;

public partial class ColorWheelPicker : UserControl
{
    private bool _isDragging;

    public static readonly DependencyProperty ColorHexProperty =
        DependencyProperty.Register(
            nameof(ColorHex),
            typeof(string),
            typeof(ColorWheelPicker),
            new FrameworkPropertyMetadata("#FFB450", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorHexChanged));

    public string ColorHex
    {
        get => (string)GetValue(ColorHexProperty);
        set => SetValue(ColorHexProperty, value);
    }

    public ColorWheelPicker()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            WheelImage.Source = ColorWheelGenerator.GetOrCreateWheel(200);
            UpdateThumbFromHex(ColorHex);
        };
    }

    private static void OnColorHexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorWheelPicker picker && !picker._isDragging && e.NewValue is string hex)
        {
            picker.UpdateThumbFromHex(hex);
        }
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        ((UIElement)sender).CaptureMouse();
        ProcessMousePosition(e.GetPosition(this));
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            ProcessMousePosition(e.GetPosition(this));
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ((UIElement)sender).ReleaseMouseCapture();
        }
    }

    private void ProcessMousePosition(Point pos)
    {
        double cx = ActualWidth / 2.0;
        double cy = ActualHeight / 2.0;
        if (cx <= 0 || cy <= 0) { cx = 60; cy = 60; }

        double radius = Math.Min(cx, cy) - 2;

        double dx = pos.X - cx;
        double dy = pos.Y - cy;
        double dist = Math.Sqrt(dx * dx + dy * dy);

        if (dist > radius)
        {
            dx = (dx / dist) * radius;
            dy = (dy / dist) * radius;
            dist = radius;
        }

        double angle = Math.Atan2(dy, dx) * (180.0 / Math.PI);
        if (angle < 0) angle += 360.0;

        double sat = Math.Clamp(dist / radius, 0.0, 1.0);

        var (r, g, b) = DeviceItemViewModel.HsvToRgb(angle, sat, 1.0);

        // Update thumb position
        double thumbX = cx + dx;
        double thumbY = cy + dy;
        Canvas.SetLeft(SelectorThumb, thumbX - (SelectorThumb.Width / 2.0));
        Canvas.SetTop(SelectorThumb, thumbY - (SelectorThumb.Height / 2.0));

        // Call ViewModel SetRgb or set ColorHex
        if (DataContext is DeviceItemViewModel vm)
        {
            vm.SetRgb(r, g, b);
        }
        else
        {
            ColorHex = $"#{r:X2}{g:X2}{b:X2}";
        }
    }

    public void UpdateThumbFromHex(string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return;
        hex = hex.Trim().TrimStart('#');
        if (hex.Length != 6) return;

        if (!byte.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out byte r) ||
            !byte.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out byte g) ||
            !byte.TryParse(hex[4..6], System.Globalization.NumberStyles.HexNumber, null, out byte b))
        {
            return;
        }

        var (h, s, _) = RgbToHsv(r, g, b);

        double cx = ActualWidth / 2.0;
        double cy = ActualHeight / 2.0;
        if (cx <= 0 || cy <= 0) { cx = 60; cy = 60; }
        double radius = Math.Min(cx, cy) - 2;

        double rad = h * (Math.PI / 180.0);
        double dist = s * radius;

        double thumbX = cx + dist * Math.Cos(rad);
        double thumbY = cy + dist * Math.Sin(rad);

        Canvas.SetLeft(SelectorThumb, thumbX - (SelectorThumb.Width / 2.0));
        Canvas.SetTop(SelectorThumb, thumbY - (SelectorThumb.Height / 2.0));
    }

    private static (double H, double S, double V) RgbToHsv(byte r, byte g, byte b)
    {
        double rd = r / 255.0;
        double gd = g / 255.0;
        double bd = b / 255.0;

        double max = Math.Max(rd, Math.Max(gd, bd));
        double min = Math.Min(rd, Math.Min(gd, bd));
        double delta = max - min;

        double h = 0;
        if (delta > 0.00001)
        {
            if (Math.Abs(max - rd) < 0.00001)
                h = 60 * (((gd - bd) / delta) % 6);
            else if (Math.Abs(max - gd) < 0.00001)
                h = 60 * (((bd - rd) / delta) + 2);
            else
                h = 60 * (((rd - gd) / delta) + 4);

            if (h < 0) h += 360;
        }

        double s = max < 0.00001 ? 0 : delta / max;
        double v = max;

        return (h, s, v);
    }
}
