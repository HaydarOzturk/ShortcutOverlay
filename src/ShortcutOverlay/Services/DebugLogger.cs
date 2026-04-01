using System.IO;
using System.Runtime.CompilerServices;

namespace ShortcutOverlay.Services;

/// <summary>
/// Simple file-based debug logger for diagnosing shortcut execution issues.
/// Writes timestamped entries to a log file next to the executable.
/// </summary>
public static class DebugLogger
{
    private static readonly string LogPath;
    private static readonly object Lock = new();

    static DebugLogger()
    {
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        LogPath = Path.Combine(exeDir, "debug_shortcut_execution.log");

        // Write a header on first load
        try
        {
            File.AppendAllText(LogPath,
                $"\n{'=',-60}\n" +
                $"  Session started: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\n" +
                $"{'=',-60}\n");
        }
        catch { /* best-effort */ }
    }

    public static void Log(string message,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string file = "")
    {
        try
        {
            var shortFile = Path.GetFileNameWithoutExtension(file);
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{shortFile}.{caller}] {message}\n";
            lock (Lock)
            {
                File.AppendAllText(LogPath, line);
            }
        }
        catch { /* never crash the app for logging */ }
    }

    /// <summary>
    /// Logs a Win32 boolean result with the API name.
    /// </summary>
    public static void LogWin32(string apiName, bool result, string context = "",
        [CallerMemberName] string caller = "",
        [CallerFilePath] string file = "")
    {
        var msg = result
            ? $"✓ {apiName} succeeded"
            : $"✗ {apiName} FAILED";
        if (!string.IsNullOrEmpty(context))
            msg += $" | {context}";
        Log(msg, caller, file);
    }
}
