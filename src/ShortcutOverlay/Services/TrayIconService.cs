using System.Windows;
using System.Windows.Controls;
using Hardcodet.NotifyIcon.Wpf;

namespace ShortcutOverlay.Services;

/// <summary>
/// Service that manages the system tray icon and its context menu.
/// </summary>
public class TrayIconService : IDisposable
{
    private TaskbarIcon? _taskbarIcon;

    public event Action? ToggleOverlayRequested;
    public event Action? OpenSettingsRequested;
    public event Action? QuitRequested;

    public void Initialize()
    {
        _taskbarIcon = new TaskbarIcon();

        // Create context menu programmatically
        var contextMenu = new ContextMenu();

        var showOverlayItem = new MenuItem { Header = "Show Overlay" };
        showOverlayItem.Click += (_, _) => ToggleOverlayRequested?.Invoke();

        var settingsItem = new MenuItem { Header = "Settings" };
        settingsItem.Click += (_, _) => OpenSettingsRequested?.Invoke();

        var separatorItem = new Separator();

        var quitItem = new MenuItem { Header = "Quit" };
        quitItem.Click += (_, _) => QuitRequested?.Invoke();

        contextMenu.Items.Add(showOverlayItem);
        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(separatorItem);
        contextMenu.Items.Add(quitItem);

        _taskbarIcon.ContextMenu = contextMenu;

        // Set a placeholder icon (generic application icon)
        // In a real implementation, you'd load a proper icon file here
        _taskbarIcon.IconSource = new System.Windows.Media.Imaging.BitmapImage(
            new Uri("pack://application:,,,/Resources/Icons/app-icon.ico", UriKind.RelativeOrAbsolute));

        // Handle left-click on the icon
        _taskbarIcon.TrayLeftMouseUp += (_, _) => ToggleOverlayRequested?.Invoke();
    }

    public void Dispose()
    {
        _taskbarIcon?.Dispose();
        _taskbarIcon = null;
    }
}
