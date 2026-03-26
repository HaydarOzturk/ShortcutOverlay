using System.Diagnostics;
using System.IO;
using System.Text;
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

    public event Action<ActiveAppInfo>? ActiveAppChanged;

    /// <summary>
    /// Starts monitoring foreground window changes. Must be called from UI thread.
    /// </summary>
    public void Start()
    {
        _hookManager.ForegroundWindowChanged += OnForegroundWindowChanged;
        _hookManager.StartHook();
    }

    private void OnForegroundWindowChanged(IntPtr hwnd)
    {
        var appInfo = GetActiveApplication(hwnd);

        // Avoid duplicate events for the same process
        if (_lastProcessName == appInfo.ProcessName)
            return;

        _lastProcessName = appInfo.ProcessName;
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

        // Get the process
        Process? process = null;
        try
        {
            process = Process.GetProcessById((int)processId);
        }
        catch
        {
            return new("unknown", "Unknown", windowTitle);
        }

        var processName = Path.GetFileNameWithoutExtension(process.ProcessName).ToLowerInvariant();

        // Special case: explorer.exe
        if (processName == "explorer")
        {
            if (windowClass == "Progman" || windowClass == "WorkerW")
                return new("desktop", "Desktop", windowTitle);

            if (windowClass == "CabinetWClass")
                return new("file-explorer", "File Explorer", windowTitle);
        }

        // Special case: applicationframehost (UWP app)
        if (processName == "applicationframehost")
        {
            var realAppName = DetectUwpApp(hwnd);
            if (!string.IsNullOrEmpty(realAppName))
                return new(realAppName, realAppName, windowTitle);
        }

        // Special case: windowsterminal
        if (processName == "windowsterminal")
        {
            var shellType = DetectTerminalShell(windowTitle);
            return new(shellType, shellType, windowTitle);
        }

        var displayName = process.MainWindowTitle ?? processName;
        process.Dispose();

        return new(processName, displayName, windowTitle);
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

    public void Dispose()
    {
        _hookManager?.Dispose();
    }
}
