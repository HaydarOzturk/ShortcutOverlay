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

        // Load and apply the user's theme from settings
        var settingsService = Services.GetRequiredService<SettingsService>();
        settingsService.LoadAsync().GetAwaiter().GetResult();
        ThemeManager.ApplyTheme(settingsService.Current.Theme);

        // Start window detection
        var detection = Services.GetRequiredService<WindowDetectionService>();
        detection.Start();

        // Get the view model
        var viewModel = Services.GetRequiredService<MainViewModel>();

        // Create and show the floating widget window (default mode)
        var window = Services.GetRequiredService<FloatingWidgetWindow>();
        window.Show();
        viewModel.ActiveOverlay = window;

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
