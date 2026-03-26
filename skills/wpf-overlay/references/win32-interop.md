# Win32 Interop Reference

Ready-to-use P/Invoke patterns for the ShortcutOverlay project. Copy these into `NativeInterop/`
and adapt as needed.

## Table of Contents

1. [Win32Api.cs — P/Invoke Declarations](#1-win32apics)
2. [WindowHookManager — SetWinEventHook Wrapper](#2-windowhookmanager)
3. [WindowDetectionService — Full Implementation](#3-windowdetectionservice)
4. [HotkeyService — Global Hotkey Registration](#4-hotkeyservice)
5. [Topmost Fix — SetWindowPos](#5-topmost-fix)
6. [UWP App Detection](#6-uwp-app-detection)
7. [Alt+Tab Hiding](#7-alt-tab-hiding)

---

## 1. Win32Api.cs

All P/Invoke declarations in one place. Nothing else in the project should use `[DllImport]`.

```csharp
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ShortcutOverlay.NativeInterop;

/// <summary>
/// Centralized Win32 API declarations. All P/Invoke lives here.
/// </summary>
public static class Win32Api
{
    // --- Window Event Hook ---

    public delegate void WinEventDelegate(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess,
        uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;

    // --- Foreground Window ---

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    // --- Global Hotkey ---

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const int WM_HOTKEY = 0x0312;

    // Modifier flags for RegisterHotKey
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    // --- Window Positioning (Topmost fix) ---

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new(-2);
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOACTIVATE = 0x0010;

    // --- Extended Window Style (Alt+Tab hiding) ---

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    // --- Child Window Enumeration (UWP detection) ---

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    // --- DWM (Mica/Acrylic) ---

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    public const int DWMSBT_MAINWINDOW = 2;       // Mica
    public const int DWMSBT_TRANSIENTWINDOW = 3;   // Acrylic
    public const int DWMSBT_TABBEDWINDOW = 4;      // Tabbed Mica
}
```

---

## 2. WindowHookManager

Wraps `SetWinEventHook` with proper delegate pinning to prevent GC collection.

```csharp
using System;

namespace ShortcutOverlay.NativeInterop;

/// <summary>
/// Manages the lifecycle of a WinEventHook. The delegate is stored as a field
/// to prevent garbage collection — this is critical, as a collected delegate
/// causes silent hook death or crashes.
/// </summary>
public sealed class WindowHookManager : IDisposable
{
    private IntPtr _hookHandle;
    // IMPORTANT: Store as field to prevent GC collection
    private readonly Win32Api.WinEventDelegate _delegate;

    public event Action<IntPtr>? ForegroundWindowChanged;

    public WindowHookManager()
    {
        // Pin the delegate by storing it as a class field
        _delegate = OnWinEvent;
    }

    public void StartHook()
    {
        if (_hookHandle != IntPtr.Zero) return;

        _hookHandle = Win32Api.SetWinEventHook(
            Win32Api.EVENT_SYSTEM_FOREGROUND,
            Win32Api.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _delegate,
            0, 0,
            Win32Api.WINEVENT_OUTOFCONTEXT);

        if (_hookHandle == IntPtr.Zero)
            throw new InvalidOperationException("Failed to set WinEventHook.");
    }

    private void OnWinEvent(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (eventType == Win32Api.EVENT_SYSTEM_FOREGROUND)
        {
            ForegroundWindowChanged?.Invoke(hwnd);
        }
    }

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            Win32Api.UnhookWinEvent(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }
}
```

---

## 3. WindowDetectionService

Full implementation with special-case handling for Desktop, File Explorer, UWP apps,
and Windows Terminal.

```csharp
using System;
using System.Diagnostics;
using System.Text;

namespace ShortcutOverlay.Services;

public record ActiveAppInfo(string ProcessName, string DisplayName, string WindowTitle);

public sealed class WindowDetectionService : IDisposable
{
    private readonly NativeInterop.WindowHookManager _hookManager;

    public event Action<ActiveAppInfo>? ActiveAppChanged;

    public WindowDetectionService()
    {
        _hookManager = new NativeInterop.WindowHookManager();
        _hookManager.ForegroundWindowChanged += OnForegroundChanged;
    }

    /// <summary>
    /// Must be called from the UI thread (needs a message loop).
    /// Call this from Window.SourceInitialized or App.OnStartup.
    /// </summary>
    public void Start() => _hookManager.StartHook();

    private void OnForegroundChanged(IntPtr hwnd)
    {
        try
        {
            var info = IdentifyWindow(hwnd);
            if (info != null)
                ActiveAppChanged?.Invoke(info);
        }
        catch
        {
            // Swallow exceptions in the hook callback to prevent crashes.
            // In production, log these.
        }
    }

    private ActiveAppInfo? IdentifyWindow(IntPtr hwnd)
    {
        NativeInterop.Win32Api.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return null;

        using var process = Process.GetProcessById((int)pid);
        var processName = process.ProcessName.ToLowerInvariant();
        var windowTitle = GetWindowTitle(hwnd);
        var className = GetWindowClassName(hwnd);

        // --- Special case: Explorer (Desktop vs File Explorer) ---
        if (processName == "explorer")
        {
            if (className is "Progman" or "WorkerW")
                return new ActiveAppInfo("desktop", "Desktop", windowTitle);
            if (className == "CabinetWClass")
                return new ActiveAppInfo("file-explorer", "File Explorer", windowTitle);
            return new ActiveAppInfo("explorer", "Explorer", windowTitle);
        }

        // --- Special case: UWP apps behind ApplicationFrameHost ---
        if (processName == "applicationframehost")
        {
            var realApp = FindUwpChildProcess(hwnd);
            if (realApp != null)
                return new ActiveAppInfo(realApp.ToLowerInvariant(), realApp, windowTitle);
            return new ActiveAppInfo("uwp-unknown", "UWP App", windowTitle);
        }

        // --- Special case: Windows Terminal (detect shell type) ---
        if (processName == "windowsterminal")
        {
            var shell = DetectShellFromTitle(windowTitle);
            return new ActiveAppInfo(shell, $"Terminal ({shell})", windowTitle);
        }

        return new ActiveAppInfo(processName, process.ProcessName, windowTitle);
    }

    private static string GetWindowClassName(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        NativeInterop.Win32Api.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        var sb = new StringBuilder(512);
        NativeInterop.Win32Api.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>
    /// Enumerates child windows of ApplicationFrameHost to find the real UWP process.
    /// </summary>
    private static string? FindUwpChildProcess(IntPtr parentHwnd)
    {
        string? result = null;

        NativeInterop.Win32Api.EnumChildWindows(parentHwnd, (childHwnd, _) =>
        {
            NativeInterop.Win32Api.GetWindowThreadProcessId(childHwnd, out uint childPid);
            try
            {
                using var childProcess = Process.GetProcessById((int)childPid);
                if (childProcess.ProcessName != "ApplicationFrameHost")
                {
                    result = childProcess.ProcessName;
                    return false; // Stop enumerating
                }
            }
            catch { /* Process may have exited */ }
            return true; // Continue enumerating
        }, IntPtr.Zero);

        return result;
    }

    /// <summary>
    /// Windows Terminal's title bar often contains the shell type.
    /// Falls back to "terminal" if it can't determine.
    /// </summary>
    private static string DetectShellFromTitle(string title)
    {
        var lower = title.ToLowerInvariant();
        if (lower.Contains("powershell") || lower.Contains("pwsh"))
            return "powershell";
        if (lower.Contains("cmd") || lower.Contains("command prompt"))
            return "cmd";
        if (lower.Contains("bash") || lower.Contains("wsl") || lower.Contains("ubuntu"))
            return "bash";
        return "terminal";
    }

    public void Dispose() => _hookManager.Dispose();
}
```

---

## 4. HotkeyService

Global hotkey registration with proper WPF message pump integration.

```csharp
using System;
using System.Windows;
using System.Windows.Interop;

namespace ShortcutOverlay.Services;

/// <summary>
/// Registers system-wide hotkeys. Must be initialized after a Window is created
/// (needs an HWND for the message hook).
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private IntPtr _windowHandle;
    private HwndSource? _source;
    private int _nextHotkeyId = 1;

    public event Action? ToggleOverlayRequested;

    /// <summary>
    /// Call from Window.SourceInitialized. Must be on the UI thread.
    /// </summary>
    public void Initialize(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _windowHandle = helper.Handle;

        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(WndProc);
    }

    /// <summary>
    /// Registers the overlay toggle hotkey. Returns false if the key combo
    /// is already taken by another app (no exception — just false).
    /// </summary>
    public bool RegisterToggleHotkey(uint modifiers, uint key)
    {
        bool success = NativeInterop.Win32Api.RegisterHotKey(
            _windowHandle, _nextHotkeyId, modifiers, key);

        if (!success)
        {
            // Hotkey is taken by another application.
            // Surface this to the user so they can pick a different combo.
            return false;
        }

        _nextHotkeyId++;
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeInterop.Win32Api.WM_HOTKEY)
        {
            ToggleOverlayRequested?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        // Unregister all hotkeys
        for (int i = 1; i < _nextHotkeyId; i++)
        {
            NativeInterop.Win32Api.UnregisterHotKey(_windowHandle, i);
        }

        _source?.RemoveHook(WndProc);
        _source?.Dispose();
    }
}
```

---

## 5. Topmost Fix

Apply this in any overlay window's `SourceInitialized` to make Topmost actually work
even with `ShowInTaskbar=False`.

```csharp
// In your overlay window code-behind or via a behavior:
protected override void OnSourceInitialized(EventArgs e)
{
    base.OnSourceInitialized(e);
    ForceTopmost();
}

private void ForceTopmost()
{
    var handle = new WindowInteropHelper(this).Handle;
    Win32Api.SetWindowPos(handle, Win32Api.HWND_TOPMOST,
        0, 0, 0, 0,
        Win32Api.SWP_NOMOVE | Win32Api.SWP_NOSIZE | Win32Api.SWP_NOACTIVATE);
}
```

---

## 6. UWP App Detection

Already integrated into `WindowDetectionService` above (see `FindUwpChildProcess`).
The key insight: `ApplicationFrameHost` is a container — enumerate its child windows
to find the real process.

---

## 7. Alt+Tab Hiding

Apply this to hide the overlay from Alt+Tab without breaking other functionality:

```csharp
// In OnSourceInitialized:
private void HideFromAltTab()
{
    var handle = new WindowInteropHelper(this).Handle;
    int exStyle = Win32Api.GetWindowLong(handle, Win32Api.GWL_EXSTYLE);
    exStyle |= Win32Api.WS_EX_TOOLWINDOW;    // Hide from Alt+Tab
    exStyle |= Win32Api.WS_EX_NOACTIVATE;    // Don't steal focus
    Win32Api.SetWindowLong(handle, Win32Api.GWL_EXSTYLE, exStyle);
}
```
