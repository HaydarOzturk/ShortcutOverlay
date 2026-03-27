using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;

namespace ShortcutOverlay.Services;

/// <summary>
/// Service that manages the system tray icon and its context menu.
/// Uses Hardcodet.NotifyIcon.Wpf for system tray integration.
/// </summary>
public class TrayIconService : IDisposable
{
    private TaskbarIcon? _taskbarIcon;

    public event Action? ToggleOverlayRequested;
    public event Action? OpenSettingsRequested;
    public event Action? QuitRequested;

    public void Initialize()
    {
        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = "Hotglass — Ctrl+Shift+S to toggle"
        };

        // Load custom app icon from embedded resource
        try
        {
            var iconUri = new Uri("pack://application:,,,/Resources/Icons/app-icon.ico", UriKind.Absolute);
            var streamInfo = Application.GetResourceStream(iconUri);
            if (streamInfo != null)
            {
                _taskbarIcon.Icon = new System.Drawing.Icon(streamInfo.Stream);
            }
            else
            {
                _taskbarIcon.Icon = System.Drawing.SystemIcons.Application;
            }
        }
        catch
        {
            // Fallback: icon may fail on some configurations, tray still works
            _taskbarIcon.Icon = System.Drawing.SystemIcons.Application;
            System.Diagnostics.Debug.WriteLine("Could not set tray icon; using default.");
        }

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

        // Handle left-click on the icon
        _taskbarIcon.TrayLeftMouseUp += (_, _) => ToggleOverlayRequested?.Invoke();
    }

    public void Dispose()
    {
        _taskbarIcon?.Dispose();
        _taskbarIcon = null;
    }
}
