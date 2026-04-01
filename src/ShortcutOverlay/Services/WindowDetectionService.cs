using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using ShortcutOverlay.Models;
using ShortcutOverlay.NativeInterop;

namespace ShortcutOverlay.Services;

/// <summary>
/// Detects the foreground window and identifies the active application.
/// Uses WindowHookManager to track window changes and resolves special cases
/// (desktop, file explorer, UWP apps, terminal shells).
/// </summary>
public sealed class WindowDetectionService : IDisposable
{
    private readonly WindowHookManager _hookManager = new();
    private string? _lastProcessName;
    private string? _ownProcessName;

    /// <summary>
    /// The window handle of the last detected foreground app (excluding our overlay).
    /// Used by ShortcutListControl to send keystrokes to the correct window.
    /// </summary>
    public IntPtr LastForegroundHwnd { get; private set; }

    public event Action<ActiveAppInfo>? ActiveAppChanged;

    /// <summary>
    /// Starts monitoring foreground window changes. Must be called from UI thread.
    /// </summary>
    public void Start()
    {
        // Cache our own process name so we can ignore self-activation events
        _ownProcessName = Path.GetFileNameWithoutExtension(
            System.Diagnostics.Process.GetCurrentProcess().ProcessName).ToLowerInvariant();

        _hookManager.ForegroundWindowChanged += OnForegroundWindowChanged;
        _hookManager.StartHook();
    }

    private void OnForegroundWindowChanged(IntPtr hwnd)
    {
        var appInfo = GetActiveApplication(hwnd);

        // Ignore when our own overlay gains focus (e.g. drag, click, search box).
        // This keeps the last detected app's shortcuts visible.
        if (appInfo.ProcessName == _ownProcessName)
            return;

        // Avoid duplicate events for the same process.
        // Exception: never deduplicate "unknown" — different elevated processes
        // all report as "unknown" and we need each transition to fire.
        if (_lastProcessName == appInfo.ProcessName && appInfo.ProcessName != "unknown")
            return;

        _lastProcessName = appInfo.ProcessName;
        LastForegroundHwnd = hwnd;
        ActiveAppChanged?.Invoke(appInfo);
    }

    /// <summary>
    /// Gets information about the active application from the given window handle.
    /// Handles special cases: desktop, file explorer, UWP apps, terminal shells.
    /// </summary>
    private static ActiveAppInfo GetActiveApplication(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return new("unknown", "Unknown", string.Empty);

        var windowTitle = GetWindowTitle(hwnd);
        var windowClass = GetWindowClass(hwnd);
        var processId = GetProcessIdFromWindow(hwnd);

        if (processId == 0)
            return new("unknown", "Unknown", windowTitle);

        // Get the process — may fail for elevated/system processes
        Process? process = null;
        string processName;
        try
        {
            process = Process.GetProcessById((int)processId);
            processName = Path.GetFileNameWithoutExtension(process.ProcessName).ToLowerInvariant();
        }
        catch
        {
            // Elevated process (e.g. Task Manager, Admin tools) — we can't access it directly.
            // Try to infer the app from the window title as a fallback.
            var inferredName = InferProcessFromTitle(windowTitle);
            return new(inferredName, ToFriendlyName(inferredName), windowTitle);
        }

        // Special case: explorer.exe
        if (processName == "explorer")
        {
            if (windowClass == "Progman" || windowClass == "WorkerW")
                return new("desktop", "Desktop", windowTitle);

            if (windowClass == "CabinetWClass")
            {
                var explorerIcon = ExtractIconFromProcess(process);
                return new("file-explorer", "File Explorer", windowTitle, explorerIcon);
            }
        }

        // Special case: applicationframehost (UWP app)
        if (processName == "applicationframehost")
        {
            var realAppName = DetectUwpApp(hwnd);
            if (!string.IsNullOrEmpty(realAppName))
                return new(realAppName, ToFriendlyName(realAppName), windowTitle);
        }

        // Special case: windowsterminal
        if (processName == "windowsterminal")
        {
            var shellType = DetectTerminalShell(windowTitle);
            var termIcon = ExtractIconFromProcess(process);
            return new(shellType, ToFriendlyName(shellType), windowTitle, termIcon);
        }

        // General case: use short friendly name, NOT the full window title
        var displayName = ToFriendlyName(processName);
        var icon = ExtractIconFromProcess(process);
        process.Dispose();

        return new(processName, displayName, windowTitle, icon);
    }

    /// <summary>
    /// Detects the real UWP app name by enumerating child windows.
    /// </summary>
    private static string DetectUwpApp(IntPtr hwnd)
    {
        var foundApps = new List<string>();

        bool EnumCallback(IntPtr childHwnd, IntPtr lParam)
        {
            var childClass = GetWindowClass(childHwnd);
            if (!string.IsNullOrEmpty(childClass) && childClass.Contains("App", StringComparison.OrdinalIgnoreCase))
                foundApps.Add(childClass);

            return true;
        }

        Win32Api.EnumChildWindows(hwnd, EnumCallback, IntPtr.Zero);

        return foundApps.FirstOrDefault() ?? "uwp-app";
    }

    /// <summary>
    /// Detects the terminal shell type (powershell, cmd, bash, terminal) from the window title.
    /// </summary>
    private static string DetectTerminalShell(string windowTitle)
    {
        if (windowTitle.Contains("PowerShell", StringComparison.OrdinalIgnoreCase))
            return "powershell";

        if (windowTitle.Contains("Command Prompt", StringComparison.OrdinalIgnoreCase) ||
            windowTitle.Contains("cmd.exe", StringComparison.OrdinalIgnoreCase))
            return "cmd";

        if (windowTitle.Contains("bash", StringComparison.OrdinalIgnoreCase) ||
            windowTitle.Contains("WSL", StringComparison.OrdinalIgnoreCase))
            return "bash";

        return "terminal";
    }

    private static uint GetProcessIdFromWindow(IntPtr hwnd)
    {
        Win32Api.GetWindowThreadProcessId(hwnd, out var processId);
        return processId;
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        Win32Api.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetWindowClass(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        Win32Api.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>
    /// Extracts the application icon from a process's executable using SHGetFileInfo.
    /// Returns null if extraction fails (e.g., elevated process, UWP, missing exe).
    /// </summary>
    private static BitmapSource? ExtractIconFromProcess(Process process)
    {
        try
        {
            var exePath = process.MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath)) return null;

            var shinfo = new Win32Api.SHFILEINFO();
            var result = Win32Api.SHGetFileInfo(
                exePath, 0, ref shinfo,
                (uint)Marshal.SizeOf(shinfo),
                Win32Api.SHGFI_ICON | Win32Api.SHGFI_SMALLICON);

            if (result == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero)
                return null;

            var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                shinfo.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            bitmapSource.Freeze(); // Make cross-thread safe
            Win32Api.DestroyIcon(shinfo.hIcon);

            return bitmapSource;
        }
        catch
        {
            // Fails for elevated/system processes — that's fine, return null
            return null;
        }
    }

    /// <summary>
    /// Attempts to identify the process from the window title when we can't
    /// access the process directly (elevated/system processes).
    /// </summary>
    private static string InferProcessFromTitle(string windowTitle)
    {
        if (string.IsNullOrEmpty(windowTitle))
            return "unknown";

        var title = windowTitle.ToLowerInvariant();

        // Well-known elevated apps
        if (title.Contains("task manager") || title.Contains("görev yöneticisi"))
            return "taskmgr";
        if (title.Contains("registry editor") || title.Contains("kayıt defteri"))
            return "regedit";
        if (title.Contains("device manager") || title.Contains("aygıt yöneticisi"))
            return "devmgmt";
        if (title.Contains("event viewer") || title.Contains("olay görüntüleyicisi"))
            return "eventvwr";
        if (title.Contains("services") && title.Length < 20)
            return "services";
        if (title.Contains("command prompt") || title.Contains("komut istemi"))
            return "cmd";
        if (title.Contains("powershell"))
            return "powershell";
        if (title.Contains("visual studio") && !title.Contains("code"))
            return "devenv";

        return "unknown";
    }

    /// <summary>
    /// Converts a process name to a short, human-friendly display name.
    /// E.g., "chrome" → "Chrome", "devenv" → "Visual Studio", "code" → "VS Code".
    /// </summary>
    private static string ToFriendlyName(string processName)
    {
        // Well-known process name → friendly name mappings
        return processName.ToLowerInvariant() switch
        {
            "devenv" => "Visual Studio",
            "code" => "VS Code",
            "msedge" => "Edge",
            "chrome" => "Chrome",
            "firefox" => "Firefox",
            "opera" => "Opera",
            "brave" => "Brave",
            "iexplore" => "Internet Explorer",
            "winword" => "Word",
            "excel" => "Excel",
            "powerpnt" => "PowerPoint",
            "onenote" => "OneNote",
            "outlook" => "Outlook",
            "msteams" => "Teams",
            "slack" => "Slack",
            "discord" => "Discord",
            "spotify" => "Spotify",
            "notepad" => "Notepad",
            "notepad++" => "Notepad++",
            "explorer" => "Explorer",
            "windowsterminal" => "Terminal",
            "powershell" => "PowerShell",
            "cmd" => "Command Prompt",
            "bash" => "Bash",
            "terminal" => "Terminal",
            "gimp-2.10" or "gimp" => "GIMP",
            "photoshop" => "Photoshop",
            "illustrator" => "Illustrator",
            "blender" => "Blender",
            "unity" => "Unity",
            "unrealEditor" => "Unreal Engine",
            "taskmgr" => "Task Manager",
            "regedit" => "Registry Editor",
            "devmgmt" => "Device Manager",
            "eventvwr" => "Event Viewer",
            "services" => "Services",
            "mmc" => "Management Console",
            _ => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(processName)
        };
    }

    public void Dispose()
    {
        _hookManager?.Dispose();
    }
}
