using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShortcutOverlay.Helpers;
using ShortcutOverlay.Models;
using ShortcutOverlay.Services;
using ShortcutOverlay.Views;

namespace ShortcutOverlay.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;

    [ObservableProperty]
    private string selectedTheme;

    [ObservableProperty]
    private double opacity;

    /// <summary>
    /// Locale-safe display string for opacity (e.g. "85%").
    /// The P0 StringFormat is broken in Turkish locale, so we compute this manually.
    /// </summary>
    public string OpacityDisplay => $"{(int)Math.Round(Opacity * 100)}%";

    [ObservableProperty]
    private string selectedDisplayMode;

    [ObservableProperty]
    private bool alwaysOnTop;

    [ObservableProperty]
    private bool startWithWindows;

    [ObservableProperty]
    private string globalHotkey;

    /// <summary>
    /// Available theme families for the combo box.
    /// </summary>
    public IReadOnlyList<string> AvailableThemes { get; }

    /// <summary>
    /// Raised when the user clicks Save and settings have been persisted.
    /// The host window should close itself.
    /// </summary>
    public event Action? SettingsSaved;

    public SettingsViewModel()
    {
        _settings = SettingsService.Instance;
        var current = _settings.Current;

        // Build theme list from palette families + Adaptive
        var themes = new List<string>();
        foreach (var family in ThemePalette.Families)
            themes.Add(family.DisplayName);
        themes.Add("Adaptive");
        AvailableThemes = themes;

        // Initialize from current settings
        selectedTheme = ParseThemeDisplay(current.Theme);
        opacity = current.Opacity;
        selectedDisplayMode = current.DisplayMode;
        alwaysOnTop = current.AlwaysOnTop;
        startWithWindows = current.StartWithWindows;
        globalHotkey = current.GlobalHotkey;
    }

    partial void OnSelectedThemeChanged(string value)
    {
        // Live preview: apply theme immediately as user selects
        var themeSetting = value.Equals("Adaptive", StringComparison.OrdinalIgnoreCase)
            ? $"adaptive:{ThemeManager.CurrentFamily}"
            : value;
        ThemeManager.ApplyTheme(themeSetting);
    }

    partial void OnOpacityChanged(double value)
    {
        OnPropertyChanged(nameof(OpacityDisplay));

        // Live preview: update opacity on all overlay windows
        foreach (var win in Application.Current?.Windows.OfType<Window>() ?? Enumerable.Empty<Window>())
        {
            if (win is IOverlayMode)
                win.Opacity = value;
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        var themeSetting = SelectedTheme.Equals("Adaptive", StringComparison.OrdinalIgnoreCase)
            ? $"adaptive:{ThemeManager.CurrentFamily}"
            : SelectedTheme;

        var newSettings = _settings.Current with
        {
            Theme = themeSetting,
            Opacity = Opacity,
            DisplayMode = SelectedDisplayMode,
            AlwaysOnTop = AlwaysOnTop,
            StartWithWindows = StartWithWindows,
            GlobalHotkey = GlobalHotkey
        };

        var previousDisplayMode = _settings.Current.DisplayMode;
        await _settings.UpdateAsync(newSettings);

        // Apply Start with Windows via registry
        ApplyStartWithWindows(StartWithWindows);

        // Sync pin state on the active overlay window
        SyncPinState(AlwaysOnTop);

        SettingsSaved?.Invoke();

        // Switch display mode if it changed (must happen after dialog closes)
        if (!string.Equals(previousDisplayMode, SelectedDisplayMode, StringComparison.OrdinalIgnoreCase))
        {
            // Dispatch so the settings dialog closes first
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                App.SwitchDisplayMode(SelectedDisplayMode);
            });
        }
    }

    /// <summary>
    /// Creates or removes the Windows startup registry entry for Hotglass.
    /// Uses HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run.
    /// </summary>
    private static void ApplyStartWithWindows(bool enable)
    {
        const string keyName = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        const string valueName = "Hotglass";

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyName, writable: true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue(valueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(valueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to update startup registry: {ex.Message}");
        }
    }

    /// <summary>
    /// Syncs the pin (position lock) state on the active overlay window
    /// so the Settings checkbox and the header pin icon stay in sync.
    /// </summary>
    private static void SyncPinState(bool isPinned)
    {
        foreach (var win in Application.Current?.Windows.OfType<Window>() ?? Enumerable.Empty<Window>())
        {
            if (win is FloatingWidgetWindow floating)
            {
                floating.SetPinState(isPinned);
            }
        }
    }

    /// <summary>
    /// Converts the stored theme string (e.g., "adaptive:Classic", "MidnightBlue")
    /// into the display name shown in the combo box.
    /// </summary>
    private static string ParseThemeDisplay(string themeSetting)
    {
        if (string.IsNullOrWhiteSpace(themeSetting) || themeSetting.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return "Classic";

        if (themeSetting.StartsWith("adaptive", StringComparison.OrdinalIgnoreCase))
            return "Adaptive";

        // Try to match family key to display name
        foreach (var f in ThemePalette.Families)
        {
            if (f.Key.Equals(themeSetting, StringComparison.OrdinalIgnoreCase))
                return f.DisplayName;
        }

        return themeSetting;
    }
}
