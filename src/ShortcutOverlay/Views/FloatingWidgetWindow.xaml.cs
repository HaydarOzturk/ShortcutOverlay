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
                TriggerAdaptiveCheck();
        };

        // Debounced timer — 400ms settle time before sampling on events
        _adaptiveDebounce = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _adaptiveDebounce.Tick += (_, _) =>
        {
            _adaptiveDebounce.Stop();
            RunAdaptiveCheck();
        };

        // Continuous polling timer — runs every 1.5s while adaptive mode is active.
        // Catches cases the event-driven approach misses:
        //   • foreground app changed but profile stayed null/same
        //   • user scrolled content behind overlay (dark → light region)
        //   • window was resized or moved behind overlay
        _adaptivePollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1500)
        };
        _adaptivePollTimer.Tick += (_, _) => RunAdaptiveCheck();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ForceTopmost();
        HideFromAltTab();
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

    /// <summary>
    /// Triggers an immediate (debounced) adaptive check — used for event-driven
    /// responses like app switch or drag-move.
    /// </summary>
    private void TriggerAdaptiveCheck()
    {
        if (!ThemeManager.IsAdaptiveMode) return;
        _adaptiveDebounce.Stop();
        _adaptiveDebounce.Start();
    }

    /// <summary>
    /// Starts the continuous poll timer. Call once after window is shown.
    /// </summary>
    private void StartAdaptivePolling()
    {
        if (ThemeManager.IsAdaptiveMode && !_adaptivePollTimer.IsEnabled)
            _adaptivePollTimer.Start();
    }

    private void StopAdaptivePolling()
    {
        _adaptivePollTimer.Stop();
    }

    /// <summary>
    /// Captures the foreground window via PrintWindow and analyzes the region
    /// where our overlay sits. ThemeAnimator handles the smooth 200ms
    /// color interpolation — no opacity tricks, no flicker.
    /// </summary>
    private void RunAdaptiveCheck()
    {
        if (!ThemeManager.IsAdaptiveMode || !_overlayVisible) return;

        try
        {
            // Get the foreground window HWND (the app behind our overlay)
            var foregroundHwnd = Win32Api.GetForegroundWindow();

            // Don't analyze our own window
            var myHwnd = new WindowInteropHelper(this).Handle;
            if (foregroundHwnd == myHwnd || foregroundHwnd == IntPtr.Zero)
                return;

            // Convert overlay position to physical screen pixels (DPI-aware)
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget == null) return;

            var transform = source.CompositionTarget.TransformToDevice;
            var topLeft = transform.Transform(new Point(Left, Top));
            var size = transform.Transform(new Point(ActualWidth, ActualHeight));

            ThemeManager.AdaptToBackground(
                foregroundHwnd,
                (int)topLeft.X, (int)topLeft.Y,
                (int)size.X, (int)size.Y);
        }
        catch
        {
            // Best-effort — don't crash if capture fails
        }
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
