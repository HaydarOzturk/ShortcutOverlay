using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ShortcutOverlay.Helpers;
using ShortcutOverlay.NativeInterop;
using ShortcutOverlay.Services;
using ShortcutOverlay.ViewModels;
using ShortcutOverlay.Views;

namespace ShortcutOverlay;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    private TrayIconService? _trayIconService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        // Register services
        services.AddSingleton<WindowDetectionService>();
        services.AddSingleton<ProfileManager>();
        services.AddSingleton<HotkeyService>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<TrayIconService>();

        // Register view models
        services.AddSingleton<MainViewModel>();

        // Register views
        services.AddTransient<FloatingWidgetWindow>();
        services.AddTransient<SidePanelWindow>();
        services.AddTransient<TrayPopupWindow>();

        Services = services.BuildServiceProvider();

        // Load settings and initialize the theme system with animated brushes
        var settingsService = Services.GetRequiredService<SettingsService>();
        settingsService.Load();
        ThemeManager.Initialize(settingsService.Current.Theme);

        // Start window detection
        var detection = Services.GetRequiredService<WindowDetectionService>();
        detection.Start();

        // Get the view model
        var viewModel = Services.GetRequiredService<MainViewModel>();

        // Create and show the correct display mode window
        var displayMode = settingsService.Current.DisplayMode;
        Window window = CreateOverlayWindow(displayMode);

        // Apply saved opacity
        if (settingsService.Current.Opacity > 0 && settingsService.Current.Opacity <= 1.0)
            window.Opacity = settingsService.Current.Opacity;

        window.Show();
        viewModel.ActiveOverlay = (IOverlayMode)window;

        // Initialize hotkey service
        var hotkeyService = Services.GetRequiredService<HotkeyService>();
        hotkeyService.Initialize(window);
        hotkeyService.RegisterToggleHotkey(Win32Api.MOD_CONTROL | Win32Api.MOD_SHIFT, 0x53); // Ctrl+Shift+S

        // Subscribe to toggle overlay requested event
        hotkeyService.ToggleOverlayRequested += () => viewModel.ActiveOverlay?.ToggleVisibility();

        // Initialize system tray icon
        _trayIconService = Services.GetRequiredService<TrayIconService>();
        _trayIconService.Initialize();
        _trayIconService.ToggleOverlayRequested += () => viewModel.ActiveOverlay?.ToggleVisibility();
        _trayIconService.OpenSettingsRequested += () => viewModel.OpenSettingsCommand.Execute(null);
        _trayIconService.QuitRequested += () => Shutdown();
    }

    /// <summary>
    /// Switches the overlay from one display mode to another.
    /// Closes the current overlay window, creates the new one, and transfers
    /// the hotkey registration.
    /// </summary>
    public static void SwitchDisplayMode(string newMode)
    {
        var viewModel = Services.GetRequiredService<MainViewModel>();
        var hotkeyService = Services.GetRequiredService<HotkeyService>();
        var settingsService = Services.GetRequiredService<SettingsService>();

        // Close current overlay
        if (viewModel.ActiveOverlay is Window oldWindow)
        {
            oldWindow.Close();
        }

        // Create the new window
        Window newWindow = CreateOverlayWindow(newMode);

        // Apply saved opacity
        if (settingsService.Current.Opacity > 0 && settingsService.Current.Opacity <= 1.0)
            newWindow.Opacity = settingsService.Current.Opacity;

        newWindow.Show();

        // Transfer hotkey to new window
        hotkeyService.Reinitialize(newWindow);

        // Set as active overlay
        viewModel.ActiveOverlay = (IOverlayMode)newWindow;
    }

    /// <summary>
    /// Creates the correct overlay window type based on the display mode string.
    /// </summary>
    private static Window CreateOverlayWindow(string displayMode)
    {
        return displayMode?.ToLowerInvariant() switch
        {
            "sidepanel" => Services.GetRequiredService<SidePanelWindow>(),
            "tray" => Services.GetRequiredService<TrayPopupWindow>(),
            _ => Services.GetRequiredService<FloatingWidgetWindow>()
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);

        _trayIconService?.Dispose();

        if (Services is IAsyncDisposable asyncDisposable)
        {
            asyncDisposable.DisposeAsync().GetAwaiter().GetResult();
        }

        (Services as IDisposable)?.Dispose();
    }
}
