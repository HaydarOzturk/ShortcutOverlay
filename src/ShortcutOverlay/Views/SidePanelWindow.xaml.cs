using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ShortcutOverlay.Helpers;
using ShortcutOverlay.NativeInterop;
using ShortcutOverlay.Services;
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

    // ── Context menu handlers ──

    private void MenuIcon_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (ContextMenu != null)
        {
            ContextMenu.PlacementTarget = sender as UIElement;
            ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            ContextMenu.IsOpen = true;
        }
    }

    private void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Tag is string tag)
        {
            var settings = SettingsService.Instance;
            string themeSetting = tag.Equals("Adaptive", StringComparison.OrdinalIgnoreCase)
                ? $"adaptive:{ThemeManager.CurrentFamily}"
                : tag;
            var newSettings = settings.Current with { Theme = themeSetting };
            settings.UpdateAsync(newSettings).ConfigureAwait(false);
            ThemeManager.ApplyTheme(themeSetting);
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

    private void DisplayModeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Tag is string tag)
        {
            System.Diagnostics.Debug.WriteLine($"Display mode switch requested: {tag}");
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("ShortcutOverlay v1.0\nA minimalist keyboard shortcut overlay.",
            "About", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Quit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
