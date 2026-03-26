namespace ShortcutOverlay.NativeInterop;

/// <summary>
/// Manages the lifecycle of a WinEventHook. The delegate is stored as a field
/// to prevent garbage collection — critical, as a collected delegate causes
/// silent hook death or crashes.
/// </summary>
public sealed class WindowHookManager : IDisposable
{
    private IntPtr _hookHandle;
    private readonly Win32Api.WinEventDelegate _delegate;

    public event Action<IntPtr>? ForegroundWindowChanged;

    public WindowHookManager()
    {
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
            throw new InvalidOperationException("Failed to set WinEventHook. Ensure this is called from the UI thread.");
    }

    private void OnWinEvent(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (eventType == Win32Api.EVENT_SYSTEM_FOREGROUND)
            ForegroundWindowChanged?.Invoke(hwnd);
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
