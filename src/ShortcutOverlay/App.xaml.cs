using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ShortcutOverlay.NativeInterop;
using ShortcutOverlay.Services;
using ShortcutOverlay.ViewModels;
using ShortcutOverlay.Views;

namespace ShortcutOverlay;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        // Register services
        services.AddSingleton<WindowDetectionService>();
        services.AddSingleton<ProfileManager>();
        services.AddSingleton<HotkeyService>();
        services.AddSingleton<SettingsService>();

        // Register view models
        services.AddSingleton<MainViewModel>();

        // Register views
        services.AddTransient<FloatingWidgetWindow>();

        Services = services.BuildServiceProvider();

        // Start window detection
        var detection = Services.GetRequiredService<WindowDetectionService>();
        detection.Start();

        // Create and show the floating widget window
        var window = Services.GetRequiredService<FloatingWidgetWindow>();
        window.Show();

        // Initialize hotkey service
        var hotkeyService = Services.GetRequiredService<HotkeyService>();
        hotkeyService.Initialize(window);
        hotkeyService.RegisterToggleHotkey(Win32Api.MOD_CONTROL | Win32Api.MOD_SHIFT, 0x53); // Ctrl+Shift+S

        // Subscribe to toggle overlay requested event
        hotkeyService.ToggleOverlayRequested += () => window.ToggleVisibility();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);

        if (Services is IAsyncDisposable asyncDisposable)
        {
            asyncDisposable.DisposeAsync().GetAwaiter().GetResult();
        }

        (Services as IDisposable)?.Dispose();
    }
}
