using System.IO;

namespace ShortcutOverlay.Helpers;

/// <summary>
/// Simple diagnostic logger for the adaptive brightness pipeline.
/// Writes to %AppData%/ShortcutOverlay/adaptive_debug.log.
/// Auto-caps at 500 entries to avoid filling disk.
/// </summary>
public static class AdaptiveDebugLog
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ShortcutOverlay", "adaptive_debug.log");

    private static int _count;
    private static bool _enabled;

    public static void Enable()
    {
        _enabled = true;
        _count = 0;
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(LogPath, $"=== Adaptive Debug Started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
        }
        catch { /* ignore */ }
    }

    public static void Log(string message)
    {
        if (!_enabled || _count > 500) return;
        _count++;
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
        }
        catch { /* ignore */ }
    }
}
