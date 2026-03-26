using System;
using System.Diagnostics;
using System.Text;
using ShortcutOverlay.NativeInterop;

namespace ShortcutOverlay.Services;

/// <summary>
/// Represents information about the currently active application window.
/// </summary>
public record ActiveAppInfo(string ProcessName, string DisplayName, string WindowTitle);

/// <summary>
/// Detects which application is currently in the foreground using Win32 event hooks.
/// Handles special cases: Desktop, File Explorer, UWP apps, and Windows Terminal.
/// </summary>
public sealed class WindowDetectionService : IDisposable
{
    private readonly WindowHookManager _hookManager;
    private ActiveAppInfo? _lastActiveApp;

    /// <summary>
    /// Raised when the active application window changes.
    /// </summary>
    public event Action<ActiveAppInfo>? ActiveAppChanged;

    public WindowDetectionService()
    {
        _hookManager = new WindowHookManager();
        _hookManager.ForegroundWindowChanged += OnForegroundChanged;
    }

    /// <summary>
    /// Starts monitoring for foreground window changes.
    /// Must be called from the UI thread (requires a message loop).
    /// Call this from Window.SourceInitialized or App.OnStartup.
    /// </summary>
    public void Start() => _hookManager.StartHook();

    private void OnForegroundChanged(IntPtr hwnd)
    {
        try
        {
            var info = IdentifyWindow(hwnd);
            if (info != null && (info.ProcessName != _lastActiveApp?.ProcessName || info.WindowTitle != _lastActiveApp?.WindowTitle))
            {
                _lastActiveApp = info;
                ActiveAppChanged?.Invoke(info);
            }
        }
        catch
        {
            // Swallow exceptions in the hook callback to prevent crashes.
            // In production, log these errors to a debug output or logging service.
        }
    }

    /// <summary>
    /// Identifies the active window, handling all special cases.
    /// </summary>
    private ActiveAppInfo? IdentifyWindow(IntPtr hwnd)
    {
        Win32Api.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return null;

        using var process = Process.GetProcessById((int)pid);
        var processName = process.ProcessName.ToLowerInvariant();
        var windowTitle = GetWindowTitle(hwnd);
        var className = GetWindowClassName(hwnd);

        // --- Special case: Explorer (Desktop vs File Explorer vs regular Explorer window) ---
        if (processName == "explorer")
        {
            // Desktop window (Progman is the traditional desktop, WorkerW is used in some Windows versions)
            if (className is "Progman" or "WorkerW")
                return new ActiveAppInfo("desktop", "Desktop", windowTitle);

            // File Explorer window
            if (className == "CabinetWClass")
                return new ActiveAppInfo("file-explorer", "File Explorer", windowTitle);

            // Other explorer.exe windows (not desktop or file explorer)
            return new ActiveAppInfo("explorer", "Explorer", windowTitle);
        }

        // --- Special case: UWP apps behind ApplicationFrameHost ---
        // UWP apps run inside a container process called ApplicationFrameHost.
        // We need to find the actual app process by enumerating child windows.
        if (processName == "applicationframehost")
        {
            var realApp = FindUwpChildProcess(hwnd);
            if (realApp != null)
                return new ActiveAppInfo(realApp.ToLowerInvariant(), realApp, windowTitle);
            return new ActiveAppInfo("uwp-unknown", "UWP App", windowTitle);
        }

        // --- Special case: Windows Terminal (detect shell type from title) ---
        // Windows Terminal can run different shells (PowerShell, CMD, bash/WSL).
        // We identify them by analyzing the title bar.
        if (processName == "windowsterminal")
        {
            var shell = DetectShellFromTitle(windowTitle);
            return new ActiveAppInfo(shell, $"Terminal ({shell})", windowTitle);
        }

        // Default case: return the process name as-is
        return new ActiveAppInfo(processName, process.ProcessName, windowTitle);
    }

    /// <summary>
    /// Retrieves the window class name using Win32 API.
    /// </summary>
    private static string GetWindowClassName(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        Win32Api.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>
    /// Retrieves the window title using Win32 API.
    /// </summary>
    private static string GetWindowTitle(IntPtr hwnd)
    {
        var sb = new StringBuilder(512);
        Win32Api.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>
    /// Enumerates child windows of ApplicationFrameHost to find the real UWP process.
    /// Returns the process name of the first non-ApplicationFrameHost child found,
    /// or null if no suitable child is found.
    /// </summary>
    private static string? FindUwpChildProcess(IntPtr parentHwnd)
    {
        string? result = null;

        Win32Api.EnumChildWindows(parentHwnd, (childHwnd, _) =>
        {
            Win32Api.GetWindowThreadProcessId(childHwnd, out uint childPid);
            try
            {
                using var childProcess = Process.GetProcessById((int)childPid);
                var childProcessName = childProcess.ProcessName.ToLowerInvariant();

                // Skip the ApplicationFrameHost process itself
                if (childProcessName != "applicationframehost")
                {
                    result = childProcess.ProcessName;
                    return false; // Stop enumerating — found the real app
                }
            }
            catch
            {
                // Process may have exited between enumeration and GetProcessById
            }
            return true; // Continue enumerating
        }, IntPtr.Zero);

        return result;
    }

    /// <summary>
    /// Analyzes the Windows Terminal window title to detect which shell is running.
    /// Windows Terminal typically includes the shell type in the title bar.
    /// Falls back to "terminal" if the shell type cannot be determined.
    /// </summary>
    private static string DetectShellFromTitle(string title)
    {
        var lower = title.ToLowerInvariant();

        // Check for PowerShell variants
        if (lower.Contains("powershell") || lower.Contains("pwsh"))
            return "powershell";

        // Check for Command Prompt
        if (lower.Contains("cmd") || lower.Contains("command prompt"))
            return "cmd";

        // Check for Bash/WSL
        if (lower.Contains("bash") || lower.Contains("wsl") || lower.Contains("ubuntu"))
            return "bash";

        // Default fallback
        return "terminal";
    }

    /// <summary>
    /// Cleans up hook resources. Call when shutting down the application.
    /// </summary>
    public void Dispose() => _hookManager.Dispose();
}
