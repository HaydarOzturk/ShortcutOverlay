using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ShortcutOverlay.NativeInterop;
using ShortcutOverlay.ViewModels;

namespace ShortcutOverlay.Views;

public partial class SidePanelWindow : Window, IOverlayMode
{
    private bool _overlayVisible = true;
    private DispatcherTimer? _autoHideTimer;
    private const double SlideOutDistance = -320;

    public bool IsOverlayVisible => _overlayVisible;

    public SidePanelWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ForceTopmost();
        HideFromAltTab();
        DockToEdge("left");
    }

    private void ForceTopmost()
    {
        var handle = new WindowInteropHelper(this).Handle;
        Win32Api.SetWindowPos(
            handle,
            Win32Api.HWND_TOPMOST,
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

    public void DockToEdge(string side)
    {
        var workArea = SystemParameters.WorkArea;

        if (side.ToLower() == "left")
        {
            Left = workArea.Left;
            Top = workArea.Top;
            Height = workArea.Height;
        }
        else if (side.ToLower() == "right")
        {
            Left = workArea.Right - Width;
            Top = workArea.Top;
            Height = workArea.Height;
        }
    }

    private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_autoHideTimer != null)
        {
            _autoHideTimer.Stop();
            _autoHideTimer = null;
        }

        SlideIn();
    }

    private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_autoHideTimer == null)
        {
            _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _autoHideTimer.Tick += (_, _) =>
            {
                _autoHideTimer.Stop();
                _autoHideTimer = null;
                SlideOut();
            };
            _autoHideTimer.Start();
        }
    }

    private void SlideIn()
    {
        var animation = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(250),
            EasingFunction = new PowerEase { EasingMode = EasingMode.EaseOut }
        };

        var transform = new System.Windows.Media.TranslateTransform();
        RenderTransform = transform;
        transform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, animation);
    }

    private void SlideOut()
    {
        var animation = new DoubleAnimation
        {
            To = SlideOutDistance,
            Duration = TimeSpan.FromMilliseconds(250),
            EasingFunction = new PowerEase { EasingMode = EasingMode.EaseIn }
        };

        var transform = new System.Windows.Media.TranslateTransform();
        RenderTransform = transform;
        transform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, animation);
    }

    public void ShowOverlay()
    {
        if (!_overlayVisible)
        {
            Show();
            Activate();
            SlideIn();
            _overlayVisible = true;
        }
    }

    public void HideOverlay()
    {
        if (_overlayVisible)
        {
            SlideOut();
            _overlayVisible = false;
            Dispatcher.BeginInvoke(new Action(() => Hide()), DispatcherPriority.Background);
        }
    }

    public void ToggleVisibility()
    {
        if (_overlayVisible)
        {
            HideOverlay();
        }
        else
        {
            ShowOverlay();
        }
    }
}
