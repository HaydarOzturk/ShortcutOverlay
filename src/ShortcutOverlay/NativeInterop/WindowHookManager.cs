using System.Windows.Threading;

namespace ShortcutOverlay.NativeInterop;

/// <summary>
/// Manages the lifecycle of a WinEventHook. The delegate is stored as a field
/// to prevent garbage collection — critical, as a collected delegate causes
/// silent hook death or crashes.
/// Also includes a fallback polling timer to catch Alt+Tab and other cases
/// where EVENT_SYSTEM_FOREGROUND fires on a transient window (task switcher).
/// </summary>
public sealed class WindowHookManager : IDisposable
{
    private IntPtr _hookHandle;
    private IntPtr _focusHookHandle;
    private readonly Win32Api.WinEventDelegate _delegate;
    private DispatcherTimer? _fallbackTimer;
    private IntPtr _lastReportedHwnd;

    public event Action<IntPtr>? ForegroundWindowChanged;

    public WindowHookManager()
    {
        _delegate = OnWinEvent;
    }

    public void StartHook()
    {
        if (_hookHandle != IntPtr.Zero) return;

        // Hook EVENT_SYSTEM_FOREGROUND — fires when a new window takes foreground
        _hookHandle = Win32Api.SetWinEventHook(
            Win32Api.EVENT_SYSTEM_FOREGROUND,
            Win32Api.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _delegate,
            0, 0,
            Win32Api.WINEVENT_OUTOFCONTEXT);

        if (_hookHandle == IntPtr.Zero)
            throw new InvalidOperationException("Failed to set WinEventHook. Ensure this is called from the UI thread.");

        // Hook EVENT_SYSTEM_MINIMIZEEND — fires when a window is restored from minimize
        _focusHookHandle = Win32Api.SetWinEventHook(
            Win32Api.EVENT_SYSTEM_MINIMIZEEND,
            Win32Api.EVENT_SYSTEM_MINIMIZEEND,
            IntPtr.Zero,
            _delegate,
            0, 0,
            Win32Api.WINEVENT_OUTOFCONTEXT);

        // Fallback polling timer: catches Alt+Tab and edge cases where the hook
        // fires on the task switcher window instead of the actual target.
        _fallbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _fallbackTimer.Tick += OnFallbackTimerTick;
        _fallbackTimer.Start();
    }

    private void OnWinEvent(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // Always check the actual foreground window (not the hwnd from the event)
        // because Alt+Tab fires the event on the task switcher, not the target window.
        var actualForeground = Win32Api.GetForegroundWindow();
        ReportIfChanged(actualForeground);
    }

    private void OnFallbackTimerTick(object? sender, EventArgs e)
    {
        var hwnd = Win32Api.GetForegroundWindow();
        ReportIfChanged(hwnd);
    }

    private void ReportIfChanged(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || hwnd == _lastReportedHwnd)
            return;

        _lastReportedHwnd = hwnd;
        ForegroundWindowChanged?.Invoke(hwnd);
    }

    public void Dispose()
    {
        _fallbackTimer?.Stop();
        _fallbackTimer = null;

        if (_hookHandle != IntPtr.Zero)
        {
            Win32Api.UnhookWinEvent(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }

        if (_focusHookHandle != IntPtr.Zero)
        {
            Win32Api.UnhookWinEvent(_focusHookHandle);
            _focusHookHandle = IntPtr.Zero;
        }
    }
}
