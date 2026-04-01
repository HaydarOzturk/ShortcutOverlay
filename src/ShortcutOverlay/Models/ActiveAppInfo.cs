using System.Windows.Media.Imaging;

namespace ShortcutOverlay.Models;

/// <summary>
/// Information about the currently active (foreground) application.
/// ProcessName is the internal key for profile matching (e.g., "chrome").
/// DisplayName is the short friendly name shown in the header (e.g., "Chrome").
/// WindowTitle is the full title bar text (only for tooltip use).
/// Icon is the extracted application icon as a WPF BitmapSource (nullable).
/// </summary>
public record ActiveAppInfo(
    string ProcessName,
    string DisplayName,
    string WindowTitle,
    BitmapSource? Icon = null);
