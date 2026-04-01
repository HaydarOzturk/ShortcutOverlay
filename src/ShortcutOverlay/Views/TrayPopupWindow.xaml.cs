using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using ShortcutOverlay.Helpers;
using ShortcutOverlay.NativeInterop;
using ShortcutOverlay.Services;
using ShortcutOverlay.ViewModels;

namespace ShortcutOverlay.Views;

public partial class TrayPopupWindow : Window, IOverlayMode
{
    private bool _overlayVisible = false;

    public bool IsOverlayVisible => _overlayVisible;

    public TrayPopupWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => PositionNearTray();
        Deactivated += Window_Deactivated;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ForceTopmost();
        HideFromAltTab();
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
        exStyle |= Win32Api.WS_EX_TOOLWINDOW;
        Win32Api.SetWindowLong(handle, Win32Api.GWL_EXSTYLE, exStyle);
    }

    public void PositionNearTray()
    {
        var workArea = SystemParameters.WorkArea;

        // Position in bottom-right, with some margin from edges
        Left = workArea.Right - Width - 10;
        Top = workArea.Bottom - Height - 10;
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        // Auto-hide when the window loses focus (user clicks elsewhere)
        HideOverlay();
    }

    public void ShowOverlay()
    {
        if (!_overlayVisible)
        {
            PositionNearTray();
            Show();
            Activate();
            _overlayVisible = true;
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

    private async void DisplayModeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Tag is string mode)
        {
            var settings = SettingsService.Instance;
            var newSettings = settings.Current with { DisplayMode = mode };
            await settings.UpdateAsync(newSettings);
            App.SwitchDisplayMode(mode);
        }
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
}
