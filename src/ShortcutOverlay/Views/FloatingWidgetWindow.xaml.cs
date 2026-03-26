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
    private readonly DispatcherTimer _adaptiveTimer;

    public bool IsOverlayVisible => _overlayVisible;

    public FloatingWidgetWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Re-check brightness when the foreground app changes
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.CurrentProfile))
                TriggerAdaptiveCheck();
        };

        // Debounced timer — 500ms settle time before sampling
        _adaptiveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _adaptiveTimer.Tick += (_, _) =>
        {
            _adaptiveTimer.Stop();
            RunAdaptiveCheck();
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ForceTopmost();
        HideFromAltTab();
        TriggerAdaptiveCheck();
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
        _adaptiveTimer.Stop();
        _adaptiveTimer.Start();
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
        }
    }

    public void HideOverlay()
    {
        if (_overlayVisible)
        {
            Hide();
            _overlayVisible = false;
        }
    }

    public void ToggleVisibility()
    {
        if (_overlayVisible) HideOverlay();
        else ShowOverlay();
    }
}
