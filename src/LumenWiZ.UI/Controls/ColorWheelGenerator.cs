using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LumenWiZ.UI.ViewModels;

namespace LumenWiZ.UI.Controls;

public static class ColorWheelGenerator
{
    private static BitmapSource? _cachedWheel;

    public static BitmapSource GetOrCreateWheel(int size = 200)
    {
        if (_cachedWheel != null) return _cachedWheel;

        int width = size;
        int height = size;
        int stride = width * 4;
        byte[] pixels = new byte[height * stride];
        double radius = size / 2.0;
        double cx = radius;
        double cy = radius;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double dx = x - cx;
                double dy = y - cy;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                int offset = (y * stride) + (x * 4);

                if (dist > radius)
                {
                    pixels[offset + 0] = 0;
                    pixels[offset + 1] = 0;
                    pixels[offset + 2] = 0;
                    pixels[offset + 3] = 0;
                }
                else
                {
                    // Antialiased outer edge
                    double alpha = dist > radius - 1.5 ? Math.Max(0.0, radius - dist) / 1.5 : 1.0;
                    byte a = (byte)(Math.Clamp(alpha, 0.0, 1.0) * 255);

                    double angle = Math.Atan2(dy, dx) * (180.0 / Math.PI);
                    if (angle < 0) angle += 360.0;

                    double sat = Math.Clamp(dist / radius, 0.0, 1.0);

                    var (r, g, b) = DeviceItemViewModel.HsvToRgb(angle, sat, 1.0);

                    pixels[offset + 0] = b;
                    pixels[offset + 1] = g;
                    pixels[offset + 2] = r;
                    pixels[offset + 3] = a;
                }
            }
        }

        var bmp = BitmapSource.Create(
            width, height, 96, 96,
            PixelFormats.Bgra32, null, pixels, stride);
        bmp.Freeze();
        _cachedWheel = bmp;
        return bmp;
    }
}
