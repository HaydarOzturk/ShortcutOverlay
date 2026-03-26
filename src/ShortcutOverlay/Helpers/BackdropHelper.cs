using System.Windows;
using System.Windows.Interop;
using ShortcutOverlay.NativeInterop;

namespace ShortcutOverlay.Helpers;

public enum BackdropType { Mica, Acrylic, MicaTabbed }

public static class BackdropHelper
{
    public static bool TryApplyBackdrop(Window window, BackdropType type)
    {
        if (Environment.OSVersion.Version.Build < 22523)
            return false;

        var handle = new WindowInteropHelper(window).Handle;
        int backdropType = type switch
        {
            BackdropType.Mica => Win32Api.DWMSBT_MAINWINDOW,
            BackdropType.Acrylic => Win32Api.DWMSBT_TRANSIENTWINDOW,
            BackdropType.MicaTabbed => Win32Api.DWMSBT_TABBEDWINDOW,
            _ => 0
        };

        int result = Win32Api.DwmSetWindowAttribute(
            handle, Win32Api.DWMWA_SYSTEMBACKDROP_TYPE,
            ref backdropType, sizeof(int));
        return result == 0;
    }
}
