using ShortcutOverlay.Helpers;
using ShortcutOverlay.NativeInterop;
using ShortcutOverlay.ViewModels;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace ShortcutOverlay.Views;

/// <summary>
/// FloatingWidgetWindow — A transparent, draggable overlay showing shortcuts
/// for the currently active application. Groups shortcuts by category.
///
/// Features:
/// - Transparent background with rounded corners
/// - Always on top (Topmost, with Win32 fix for ShowInTaskbar=False)
/// - Draggable by clicking the header
/// - Hidden from Alt+Tab
/// - Fade in/out animations
/// - Implements IOverlayMode for seamless mode switching
/// </summary>
public partial class FloatingWidgetWindow : Window, IOverlayMode
{
    public FloatingWidgetWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>
    /// Called after the window handle is created. Sets up Win32 interop fixes:
    /// 1. Forces the window to stay on top (SetWindowPos fix for Topmost + ShowInTaskbar=False)
    /// 2. Hides the window from Alt+Tab by setting WS_EX_TOOLWINDOW
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ForceTopmost();
        HideFromAltTab();
    }

    /// <summary>
    /// Allows dragging the window by clicking anywhere on it.
    /// Called when the left mouse button is pressed on the window.
    /// </summary>
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    /// <summary>
    /// Makes the window stay on top by calling SetWindowPos with HWND_TOPMOST.
    /// This is necessary because WPF's Topmost=True doesn't work correctly
    /// when ShowInTaskbar=False (WPF creates a hidden owner window that doesn't get the flag).
    ///
    /// See SKILL.md "Critical Gotcha #1: Topmost + ShowInTaskbar=False is broken"
    /// </summary>
    private void ForceTopmost()
    {
        var handle = new WindowInteropHelper(this).Handle;
        Win32Api.SetWindowPos(
            handle,
            Win32Api.HWND_TOPMOST,
            0, 0, 0, 0,
            Win32Api.SWP_NOMOVE | Win32Api.SWP_NOSIZE | Win32Api.SWP_NOACTIVATE);
    }

    /// <summary>
    /// Hides the overlay window from Alt+Tab by setting the WS_EX_TOOLWINDOW
    /// extended window style. Also sets WS_EX_NOACTIVATE to prevent stealing focus.
    ///
    /// See SKILL.md "Critical Gotcha #9: Alt+Tab visibility"
    /// </summary>
    private void HideFromAltTab()
    {
        var handle = new WindowInteropHelper(this).Handle;
        int exStyle = Win32Api.GetWindowLong(handle, Win32Api.GWL_EXSTYLE);
        exStyle |= Win32Api.WS_EX_TOOLWINDOW;  // Hide from Alt+Tab
        exStyle |= Win32Api.WS_EX_NOACTIVATE;  // Don't steal focus
        Win32Api.SetWindowLong(handle, Win32Api.GWL_EXSTYLE, exStyle);
    }

    // --- IOverlayMode Implementation ---

    /// <summary>
    /// Shows the overlay with a fade-in animation.
    /// </summary>
    public void ShowOverlay()
    {
        Show();
        AnimationHelper.FadeIn(this, durationMs: 200);
    }

    /// <summary>
    /// Hides the overlay with a fade-out animation.
    /// </summary>
    public void HideOverlay()
    {
        AnimationHelper.FadeOut(this, durationMs: 200, onComplete: () => Hide());
    }

    /// <summary>
    /// Toggles the overlay visibility between shown and hidden.
    /// </summary>
    public void ToggleVisibility()
    {
        if (IsVisible)
            HideOverlay();
        else
            ShowOverlay();
    }
}
