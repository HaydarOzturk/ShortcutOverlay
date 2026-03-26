using System.Windows;
using System.Windows.Interop;
using ShortcutOverlay.NativeInterop;

namespace ShortcutOverlay.Services;

/// <summary>
/// Registers and manages global hotkeys via Win32Api.RegisterHotKey.
/// Must be initialized with a Window handle from SourceInitialized.
/// Hooks WndProc via HwndSource to intercept WM_HOTKEY messages.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private IntPtr _windowHandle;
    private HwndSource? _hwndSource;
    private int _nextHotkeyId = 1;
    private readonly Dictionary<int, (uint modifiers, uint key)> _registeredHotkeys = new();

    public event Action? ToggleOverlayRequested;

    /// <summary>
    /// Initializes the hotkey service with the given window.
    /// Must be called from SourceInitialized event on the main window.
    /// </summary>
    public void Initialize(Window window)
    {
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        var source = PresentationSource.FromVisual(window) as HwndSource;
        if (source == null)
            throw new InvalidOperationException("Window must be initialized (SourceInitialized event)");

        _windowHandle = source.Handle;
        _hwndSource = source;
        _hwndSource.AddHook(WndProc);
    }

    /// <summary>
    /// Registers a global hotkey. Returns false if the hotkey is already in use by another application.
    /// </summary>
    public bool RegisterToggleHotkey(uint modifiers, uint key)
    {
        if (_windowHandle == IntPtr.Zero)
            throw new InvalidOperationException("HotkeyService must be initialized first");

        var hotkeyId = _nextHotkeyId++;

        if (!Win32Api.RegisterHotKey(_windowHandle, hotkeyId, modifiers, key))
        {
            // Hotkey registration failed (likely already registered by another app)
            _nextHotkeyId--;
            return false;
        }

        _registeredHotkeys[hotkeyId] = (modifiers, key);
        return true;
    }

    /// <summary>
    /// Unregisters a specific hotkey by its ID.
    /// </summary>
    private bool UnregisterHotkey(int hotkeyId)
    {
        if (_windowHandle == IntPtr.Zero)
            return false;

        var unregistered = Win32Api.UnregisterHotKey(_windowHandle, hotkeyId);
        if (unregistered)
            _registeredHotkeys.Remove(hotkeyId);

        return unregistered;
    }

    /// <summary>
    /// Window message handler that intercepts WM_HOTKEY messages.
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32Api.WM_HOTKEY)
        {
            var hotkeyId = (int)(long)wParam;

            if (_registeredHotkeys.ContainsKey(hotkeyId))
            {
                ToggleOverlayRequested?.Invoke();
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Unregisters all hotkeys and cleans up resources.
    /// </summary>
    public void Dispose()
    {
        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }

        var hotkeys = _registeredHotkeys.Keys.ToList();
        foreach (var hotkeyId in hotkeys)
        {
            UnregisterHotkey(hotkeyId);
        }

        _registeredHotkeys.Clear();
        _windowHandle = IntPtr.Zero;
    }
}
