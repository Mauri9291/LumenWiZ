using System;
using System.Runtime.InteropServices;

namespace LumenWiZ.UI.Common;

public static class WindowBackdropHelper
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    // Corner preferences: 0 = Default, 1 = DoNotRound, 2 = Round, 3 = RoundSmall
    private const int DWMWCP_ROUND = 2;

    // Backdrop types: 0 = Auto, 1 = None, 2 = Mica, 3 = Acrylic, 4 = Tabbed (Mica Alt)
    private const int DWMSBT_MAINWINDOW = 2; // Mica
    private const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic
    private const int DWMSBT_TABBEDWINDOW = 4; // Mica Alt

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static void ApplyMicaOrAcrylic(IntPtr hwnd, bool darkMode = true)
    {
        try
        {
            // 1. Force Immersive Dark Mode for title bar and caption frame
            int dark = darkMode ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

            // 2. Request native Windows 11 rounded corners
            int cornerPref = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));

            // 3. Try Mica (2)
            int backdrop = DWMSBT_MAINWINDOW;
            int res = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
            if (res != 0)
            {
                // Fallback to Acrylic (3)
                backdrop = DWMSBT_TRANSIENTWINDOW;
                DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
            }
        }
        catch { }
    }
}
