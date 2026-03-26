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
            ToolTipText = "ShortcutOverlay — Ctrl+Shift+S to toggle"
        };

        // Use the default application icon
        try
        {
            _taskbarIcon.Icon = System.Drawing.SystemIcons.Application;
        }
        catch
        {
            // Fallback: icon may fail on some configurations, tray still works
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
