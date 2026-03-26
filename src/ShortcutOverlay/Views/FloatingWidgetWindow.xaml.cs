using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using ShortcutOverlay.Helpers;
using ShortcutOverlay.NativeInterop;
using ShortcutOverlay.ViewModels;

namespace ShortcutOverlay.Views;

public partial class FloatingWidgetWindow : Window, IOverlayMode
{
    private bool _overlayVisible = true;
    private readonly DispatcherTimer _adaptiveDebounce;
    private readonly DispatcherTimer _adaptivePollTimer;

    public bool IsOverlayVisible => _overlayVisible;

    public FloatingWidgetWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

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
        ForceTopmost();
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

    private void HideFromAltTab()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var exStyle = Win32Api.GetWindowLong(handle, Win32Api.GWL_EXSTYLE);
        exStyle |= Win32Api.WS_EX_TOOLWINDOW | Win32Api.WS_EX_NOACTIVATE;
        Win32Api.SetWindowLong(handle, Win32Api.GWL_EXSTYLE, exStyle);
    }

    private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
        TriggerAdaptiveCheck();
    }

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
            _overlayVisible = true;
            TriggerAdaptiveCheck();
            StartAdaptivePolling();
        }
    }

    public void HideOverlay()
    {
        if (_overlayVisible)
        {
            Hide();
            _overlayVisible = false;
            StopAdaptivePolling();
        }
    }

    public void ToggleVisibility()
    {
        if (_overlayVisible) HideOverlay();
        else ShowOverlay();
    }
}
