using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using ShortcutOverlay.Helpers;
using ShortcutOverlay.NativeInterop;
using ShortcutOverlay.Services;
using ShortcutOverlay.ViewModels;

namespace ShortcutOverlay.Views;

public partial class FloatingWidgetWindow : Window, IOverlayMode
{
    private bool _overlayVisible = true;
    private bool _isPinned = true; // Always-on-top state
    private readonly DispatcherTimer _adaptiveDebounce;
    private readonly DispatcherTimer _adaptivePollTimer;

    public bool IsOverlayVisible => _overlayVisible;

    /// <summary>
    /// Exposes the pinned state so ShortcutListControl can bind to it
    /// and enable click-to-execute in pin mode.
    /// </summary>
    public bool IsPinnedMode => _isPinned;

    /// <summary>
    /// Sets the pin (position lock) state from external code (e.g., Settings dialog).
    /// Updates both internal state and the pin icon visual.
    /// </summary>
    public void SetPinState(bool pinned)
    {
        _isPinned = pinned;
        PinIcon.Text = _isPinned ? "📌" : "📍";
        PinIcon.ToolTip = _isPinned ? "Unpin to allow dragging" : "Pin to lock position";
        ShortcutList.IsPinned = _isPinned;
    }

    public FloatingWidgetWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Load pinned state from persisted settings
        _isPinned = SettingsService.Instance.Current.AlwaysOnTop;

        // Sync the pin icon visual and shortcut list pin state on load
        Loaded += (_, _) =>
        {
            PinIcon.Text = _isPinned ? "📌" : "📍";
            PinIcon.ToolTip = _isPinned ? "Unpin to allow dragging" : "Pin to lock position";
            ShortcutList.IsPinned = _isPinned;
        };

        // Re-check brightness when the foreground app changes (profile or app name)
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.CurrentProfile) ||
                args.PropertyName == nameof(MainViewModel.CurrentAppName))
            {
                AdaptiveDebugLog.Log($"PropertyChanged: {args.PropertyName} — triggering adaptive check");
                TriggerAdaptiveCheck();
            }
        };

        // Debounced timer — 200ms settle before sampling (enough to avoid flicker)
        _adaptiveDebounce = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _adaptiveDebounce.Tick += (_, _) =>
        {
            _adaptiveDebounce.Stop();
            RunAdaptiveCheck();
        };

        // Continuous polling timer — runs every 2s while adaptive mode is active
        // Kept gentle to avoid flicker; event-driven checks handle app switches
        _adaptivePollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(2000)
        };
        _adaptivePollTimer.Tick += (_, _) =>
        {
            // Don't poll while a transition is still running — would cause flicker
            if (!ThemeAnimator.IsTransitioning)
                RunAdaptiveCheck();
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ForceTopmost(); // Overlay ALWAYS stays on top — pin only controls position lock
        HideFromAltTab();

        var myHwnd = new WindowInteropHelper(this).Handle;
        AdaptiveDebugLog.Log($"OnSourceInitialized: myHwnd=0x{myHwnd:X}, IsAdaptiveMode={ThemeManager.IsAdaptiveMode}");

        TriggerAdaptiveCheck();
        StartAdaptivePolling();
    }

    private void ForceTopmost()
    {
        var handle = new WindowInteropHelper(this).Handle;
        Win32Api.SetWindowPos(handle, Win32Api.HWND_TOPMOST,
            0, 0, 0, 0,
            Win32Api.SWP_NOMOVE | Win32Api.SWP_NOSIZE | Win32Api.SWP_NOACTIVATE);
    }

    private void RemoveTopmost()
    {
        var handle = new WindowInteropHelper(this).Handle;
        Win32Api.SetWindowPos(handle, Win32Api.HWND_NOTOPMOST,
            0, 0, 0, 0,
            Win32Api.SWP_NOMOVE | Win32Api.SWP_NOSIZE | Win32Api.SWP_NOACTIVATE);
    }

    private void HideFromAltTab()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var exStyle = Win32Api.GetWindowLong(handle, Win32Api.GWL_EXSTYLE);
        // Only WS_EX_TOOLWINDOW to hide from Alt+Tab — WS_EX_NOACTIVATE is removed
        // so the window can receive keyboard focus (needed for search box input).
        exStyle |= Win32Api.WS_EX_TOOLWINDOW;
        Win32Api.SetWindowLong(handle, Win32Api.GWL_EXSTYLE, exStyle);
    }

    private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // When pinned, lock position — flash the pin icon to indicate locked state
        if (_isPinned)
        {
            FlashPinIcon();
            return;
        }

        DragMove();

        // After DragMove() returns (blocking call), run adaptive check immediately
        // — skip debounce since the drag is already complete and position is final.
        RunAdaptiveCheck();

        // Persist the new position
        var settings = SettingsService.Instance;
        settings.UpdateAsync(settings.Current with
        {
            FloatingPosition = new Models.PositionDto(Left, Top)
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Quick flash animation on the pin icon: dim → bright → normal over 400ms.
    /// Uses DispatcherTimer to avoid WPF animation issues with frozen brushes.
    /// </summary>
    private void FlashPinIcon()
    {
        PinIcon.Opacity = 0.3;
        PinIcon.ToolTip = "Unpin to move";

        var flashTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        int step = 0;
        flashTimer.Tick += (_, _) =>
        {
            step++;
            if (step == 1)
                PinIcon.Opacity = 1.0;
            else
            {
                PinIcon.Opacity = 1.0;
                PinIcon.ToolTip = "Unpin to allow dragging";
                flashTimer.Stop();
            }
        };
        flashTimer.Start();
    }

    // ── Icon tray click handlers ──

    private void PinIcon_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
        _isPinned = !_isPinned;
        PinIcon.Text = _isPinned ? "📌" : "📍";
        PinIcon.ToolTip = _isPinned ? "Unpin to allow dragging" : "Pin to lock position";

        // Pin only controls position lock — overlay always stays on top.
        // No topmost toggle here.

        // Persist the setting
        var settings = SettingsService.Instance;
        settings.UpdateAsync(settings.Current with { AlwaysOnTop = _isPinned }).ConfigureAwait(false);
    }

    private void MenuIcon_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (ContextMenu != null)
        {
            ContextMenu.PlacementTarget = MenuIcon;
            ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            ContextMenu.IsOpen = true;
        }
    }

    // ── Context menu handlers ──

    private void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Tag is string tag)
        {
            var settings = SettingsService.Instance;
            string themeSetting;

            if (tag.Equals("Adaptive", StringComparison.OrdinalIgnoreCase))
            {
                // Adaptive mode uses current family
                themeSetting = $"adaptive:{ThemeManager.CurrentFamily}";
            }
            else
            {
                themeSetting = tag;
            }

            var newSettings = settings.Current with { Theme = themeSetting };
            settings.UpdateAsync(newSettings).ConfigureAwait(false);
            ThemeManager.ApplyTheme(themeSetting);

            // Start or stop the adaptive poll timer based on new theme mode
            if (ThemeManager.IsAdaptiveMode)
            {
                StartAdaptivePolling();
                TriggerAdaptiveCheck();
            }
            else
            {
                StopAdaptivePolling();
            }
        }
    }

    private void OpacityMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Tag is string tag && double.TryParse(tag, out double opacity))
        {
            Opacity = opacity;
            var settings = SettingsService.Instance;
            var newSettings = settings.Current with { Opacity = opacity };
            settings.UpdateAsync(newSettings).ConfigureAwait(false);
        }
    }

    private async void DisplayModeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Tag is string mode)
        {
            // Persist the new display mode
            var settings = SettingsService.Instance;
            var newSettings = settings.Current with { DisplayMode = mode };
            await settings.UpdateAsync(newSettings);

            // Switch to the new display mode
            App.SwitchDisplayMode(mode);
        }
    }

    private void EditShortcuts_Click(object sender, RoutedEventArgs e)
    {
        var editor = new ShortcutEditorWindow();
        editor.Show();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.OpenSettingsCommand.Execute(null);
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Hotglass v1.0\nAn interactive keyboard shortcut overlay for Windows.",
            "About", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Quit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    // ── Adaptive theme logic (unchanged) ──

    private void TriggerAdaptiveCheck()
    {
        if (!ThemeManager.IsAdaptiveMode) return;
        _adaptiveDebounce.Stop();
        _adaptiveDebounce.Start();
    }

    private void StartAdaptivePolling()
    {
        if (ThemeManager.IsAdaptiveMode && !_adaptivePollTimer.IsEnabled)
        {
            AdaptiveDebugLog.Log("StartAdaptivePolling: Starting 2s poll timer");
            _adaptivePollTimer.Start();
        }
    }

    private void StopAdaptivePolling()
    {
        _adaptivePollTimer.Stop();
    }

    // Window classes that belong to the desktop shell — when these are foreground,
    // the user is looking at the desktop/wallpaper, not an app.
    private static readonly HashSet<string> ShellWindowClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW",
        "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
        "NotifyIconOverflowWindow",
        "Windows.UI.Core.CoreWindow",
    };

    /// <summary>
    /// Core adaptive check. Gets the foreground window, finds the actual app
    /// window (not our overlay), and passes it to ThemeManager for brightness analysis.
    /// Logs every bail-out point for diagnostics.
    /// </summary>
    private void RunAdaptiveCheck()
    {
        if (!ThemeManager.IsAdaptiveMode)
        {
            AdaptiveDebugLog.Log("RunAdaptiveCheck: SKIP — IsAdaptiveMode=false");
            return;
        }
        if (!_overlayVisible)
        {
            AdaptiveDebugLog.Log("RunAdaptiveCheck: SKIP — overlay not visible");
            return;
        }

        try
        {
            var foregroundHwnd = Win32Api.GetForegroundWindow();
            var myHwnd = new WindowInteropHelper(this).Handle;

            AdaptiveDebugLog.Log($"RunAdaptiveCheck: foreground=0x{foregroundHwnd:X}, myHwnd=0x{myHwnd:X}");

            // If our overlay is the foreground or we got nothing, resolve the real window
            if (foregroundHwnd == myHwnd || foregroundHwnd == IntPtr.Zero)
            {
                // Try desktop FIRST — it's the most common case when all windows are minimized
                foregroundHwnd = FindDesktopWindow();
                if (foregroundHwnd != IntPtr.Zero)
                {
                    AdaptiveDebugLog.Log($"  Resolved to desktop=0x{foregroundHwnd:X}");
                }
                else
                {
                    foregroundHwnd = FindWindowBelowUs(myHwnd);
                    AdaptiveDebugLog.Log($"  Z-order fallback=0x{foregroundHwnd:X}");
                }

                if (foregroundHwnd == IntPtr.Zero)
                {
                    AdaptiveDebugLog.Log("  SKIP — no window found");
                    return;
                }
            }

            // KEY FIX: When the foreground is any shell window (taskbar, tray, start menu),
            // redirect to the Progman desktop handle. This ensures:
            //   1. The cache key becomes "__desktop__" (not "explorer")
            //   2. Brightness sampling reads the wallpaper area, not the taskbar
            if (IsShellWindow(foregroundHwnd))
            {
                var desktopHwnd = FindDesktopWindow();
                if (desktopHwnd != IntPtr.Zero)
                {
                    AdaptiveDebugLog.Log($"  Shell window detected, redirecting to desktop=0x{desktopHwnd:X}");
                    foregroundHwnd = desktopHwnd;
                }
            }

            // Convert overlay position to physical screen pixels (DPI-aware)
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget == null)
            {
                AdaptiveDebugLog.Log("  SKIP — PresentationSource or CompositionTarget is null");
                return;
            }

            var transform = source.CompositionTarget.TransformToDevice;
            var topLeft = transform.Transform(new Point(Left, Top));
            var size = transform.Transform(new Point(ActualWidth, ActualHeight));

            AdaptiveDebugLog.Log($"  Overlay at screen ({(int)topLeft.X},{(int)topLeft.Y}) size ({(int)size.X},{(int)size.Y})");

            ThemeManager.AdaptToBackground(
                foregroundHwnd,
                (int)topLeft.X, (int)topLeft.Y,
                (int)size.X, (int)size.Y);
        }
        catch (Exception ex)
        {
            AdaptiveDebugLog.Log($"  EXCEPTION: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if a window handle belongs to any desktop/shell window class.
    /// </summary>
    private static bool IsShellWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        var className = new StringBuilder(256);
        Win32Api.GetClassName(hwnd, className, 256);
        return ShellWindowClasses.Contains(className.ToString());
    }

    /// <summary>
    /// Finds the desktop window (Progman). Used as fallback when no regular window is
    /// below the overlay — i.e., all windows are minimized and only the wallpaper is visible.
    /// </summary>
    private static IntPtr FindDesktopWindow()
    {
        // Progman is the main desktop window that hosts the wallpaper
        var progman = Win32Api.FindWindow("Progman", null);
        if (progman != IntPtr.Zero) return progman;

        // WorkerW is an alternative desktop host (used when wallpaper slideshow is active)
        var workerW = Win32Api.FindWindow("WorkerW", null);
        return workerW;
    }

    /// <summary>
    /// Walks the Z-order starting from our window to find the first visible
    /// window that isn't ours. Used when GetForegroundWindow returns our HWND.
    /// </summary>
    private static IntPtr FindWindowBelowUs(IntPtr ourHwnd)
    {
        var hwnd = ourHwnd;
        for (int i = 0; i < 50; i++) // Increased limit — many hidden shell windows
        {
            hwnd = Win32Api.GetWindow(hwnd, Win32Api.GW_HWNDNEXT);
            if (hwnd == IntPtr.Zero) break;
            if (hwnd == ourHwnd) continue;

            // Skip invisible windows
            if (!Win32Api.IsWindowVisible(hwnd)) continue;

            // Check it has a reasonable size (skip tiny/zero-size windows)
            if (Win32Api.GetWindowRect(hwnd, out var rect) && rect.Width > 100 && rect.Height > 100)
                return hwnd;
        }
        return IntPtr.Zero;
    }

    public void ShowOverlay()
    {
        if (!_overlayVisible)
        {
            Show();
            Activate();
            ForceTopmost(); // Re-assert topmost after show
            _overlayVisible = true;
            TriggerAdaptiveCheck();
            StartAdaptivePolling();
        }
    }

    public void HideOverlay()
    {
        if (_overlayVisible)
        {
            // Close any child dialogs (Settings, Shortcut Editor) before hiding
            CloseChildDialogs();

            Hide();
            _overlayVisible = false;
            StopAdaptivePolling();
        }
    }

    /// <summary>
    /// Closes all child dialog windows (Settings, Shortcut Editor) owned by
    /// or associated with this overlay. Prevents orphaned dialogs when
    /// the overlay is hidden via hotkey.
    /// </summary>
    private static void CloseChildDialogs()
    {
        var toClose = new List<Window>();
        foreach (var win in Application.Current.Windows.OfType<Window>())
        {
            if (win is SettingsWindow || win is ShortcutEditorWindow)
                toClose.Add(win);
        }
        foreach (var win in toClose)
        {
            try { win.Close(); } catch { /* ignore */ }
        }
    }

    public void ToggleVisibility()
    {
        if (_overlayVisible) HideOverlay();
        else ShowOverlay();
    }
}
