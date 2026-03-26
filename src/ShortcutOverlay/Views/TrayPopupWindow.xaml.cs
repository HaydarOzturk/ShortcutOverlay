using System.Windows;
using System.Windows.Interop;
using ShortcutOverlay.NativeInterop;
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
        exStyle |= Win32Api.WS_EX_TOOLWINDOW | Win32Api.WS_EX_NOACTIVATE;
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
}
