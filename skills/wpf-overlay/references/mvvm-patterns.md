# MVVM Patterns Reference

CommunityToolkit.Mvvm setup and patterns for the ShortcutOverlay project.

## Table of Contents

1. [DI Container Setup](#1-di-container-setup)
2. [IOverlayMode Interface](#2-ioverlaymode-interface)
3. [MainViewModel](#3-mainviewmodel)
4. [ProfileManager Service](#4-profilemanager-service)
5. [Data Models](#5-data-models)
6. [Settings DTO Pattern](#6-settings-dto-pattern)

---

## 1. DI Container Setup

Configure in `App.xaml.cs`. All services are singletons — the app has a single lifecycle.

```csharp
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace ShortcutOverlay;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Single-instance guard
        if (!SingleInstanceGuard.TryAcquire())
        {
            Shutdown();
            return;
        }

        var services = new ServiceCollection();

        // Services
        services.AddSingleton<WindowDetectionService>();
        services.AddSingleton<ProfileManager>();
        services.AddSingleton<HotkeyService>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<TrayIconService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddTransient<ProfileEditorViewModel>();

        // Views (transient — may recreate when switching display modes)
        services.AddTransient<FloatingWidgetWindow>();
        services.AddTransient<SidePanelWindow>();
        services.AddTransient<TrayPopupWindow>();

        Services = services.BuildServiceProvider();

        // Start the detection service on the UI thread
        var detection = Services.GetRequiredService<WindowDetectionService>();
        detection.Start();

        // Show initial overlay based on saved preference
        var settings = Services.GetRequiredService<SettingsService>();
        ShowOverlay(settings.Current.DisplayMode);

        base.OnStartup(e);
    }

    private void ShowOverlay(string displayMode)
    {
        Window window = displayMode switch
        {
            "sidepanel" => Services.GetRequiredService<SidePanelWindow>(),
            "tray" => Services.GetRequiredService<TrayPopupWindow>(),
            _ => Services.GetRequiredService<FloatingWidgetWindow>(),
        };
        window.Show();
    }
}
```

---

## 2. IOverlayMode Interface

All three display modes implement this so switching is seamless.

```csharp
namespace ShortcutOverlay.Views;

/// <summary>
/// Implemented by each display mode window. Enables the MainViewModel
/// to control visibility/position without knowing which mode is active.
/// </summary>
public interface IOverlayMode
{
    void ShowOverlay();
    void HideOverlay();
    void ToggleVisibility();
    bool IsVisible { get; }
}
```

---

## 3. MainViewModel

The orchestrator. Subscribes to window changes, loads profiles, exposes data for binding.
Uses CommunityToolkit.Mvvm source generators to eliminate boilerplate.

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;

namespace ShortcutOverlay.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly WindowDetectionService _detection;
    private readonly ProfileManager _profiles;
    private readonly SettingsService _settings;

    // Source-generated property: backing field + INotifyPropertyChanged
    [ObservableProperty]
    private ShortcutProfile? _currentProfile;

    [ObservableProperty]
    private string _currentAppName = "No app detected";

    [ObservableProperty]
    private string _searchFilter = string.Empty;

    // Filtered view of shortcuts based on search
    public ObservableCollection<ShortcutCategory> FilteredCategories { get; } = new();

    public MainViewModel(
        WindowDetectionService detection,
        ProfileManager profiles,
        SettingsService settings)
    {
        _detection = detection;
        _profiles = profiles;
        _settings = settings;

        _detection.ActiveAppChanged += OnActiveAppChanged;
    }

    private void OnActiveAppChanged(ActiveAppInfo info)
    {
        // This fires on the UI thread (SetWinEventHook callback)
        CurrentAppName = info.DisplayName;

        var profile = _profiles.GetProfileForProcess(info.ProcessName);
        if (profile != null && profile.ProfileId != CurrentProfile?.ProfileId)
        {
            CurrentProfile = profile;
            ApplyFilter();
        }
    }

    // Called when SearchFilter changes (via partial method from source generator)
    partial void OnSearchFilterChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredCategories.Clear();
        if (CurrentProfile == null) return;

        foreach (var category in CurrentProfile.Categories)
        {
            if (string.IsNullOrWhiteSpace(SearchFilter))
            {
                FilteredCategories.Add(category);
                continue;
            }

            var filtered = category.Shortcuts
                .Where(s => s.Description.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase)
                         || s.Keys.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase)
                         || s.Tags.Any(t => t.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (filtered.Count > 0)
            {
                FilteredCategories.Add(new ShortcutCategory
                {
                    Name = category.Name,
                    Shortcuts = filtered
                });
            }
        }
    }

    [RelayCommand]
    private void ClearSearch() => SearchFilter = string.Empty;

    [RelayCommand]
    private void OpenSettings()
    {
        // Launch settings window
        var settingsWindow = App.Services.GetRequiredService<SettingsWindow>();
        settingsWindow.ShowDialog();
    }
}
```

---

## 4. ProfileManager Service

Loads JSON profiles from disk, maps process names to profiles, caches for fast lookup.

```csharp
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ShortcutOverlay.Services;

public class ProfileManager
{
    private readonly Dictionary<string, ShortcutProfile> _profilesByProcess = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _profilesDir;
    private readonly JsonSerializerOptions _jsonOptions;

    public IReadOnlyCollection<ShortcutProfile> AllProfiles => _profilesByProcess.Values
        .DistinctBy(p => p.ProfileId).ToList();

    public ProfileManager()
    {
        _profilesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ShortcutOverlay", "profiles");

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        Directory.CreateDirectory(_profilesDir);
        LoadAllProfiles();
    }

    private void LoadAllProfiles()
    {
        foreach (var file in Directory.GetFiles(_profilesDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var profile = JsonSerializer.Deserialize<ShortcutProfile>(json, _jsonOptions);
                if (profile == null) continue;

                foreach (var processName in profile.ProcessNames)
                {
                    _profilesByProcess[processName.ToLowerInvariant()] = profile;
                }
            }
            catch
            {
                // Skip corrupted files — log in production
            }
        }
    }

    public ShortcutProfile? GetProfileForProcess(string processName)
    {
        _profilesByProcess.TryGetValue(processName.ToLowerInvariant(), out var profile);
        return profile;
    }

    public async Task SaveProfileAsync(ShortcutProfile profile)
    {
        var path = Path.Combine(_profilesDir, $"{profile.ProfileId}.json");
        var json = JsonSerializer.Serialize(profile, _jsonOptions);
        await File.WriteAllTextAsync(path, json);

        // Update cache
        foreach (var processName in profile.ProcessNames)
        {
            _profilesByProcess[processName.ToLowerInvariant()] = profile;
        }
    }
}
```

---

## 5. Data Models

Plain C# records — no WPF types here (keeps them serializable).

```csharp
using System.Collections.Generic;

namespace ShortcutOverlay.Models;

public record ShortcutProfile
{
    public string ProfileId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public List<string> ProcessNames { get; init; } = new();
    public List<string> WindowClasses { get; init; } = new();
    public string Icon { get; init; } = string.Empty;
    public List<ShortcutCategory> Categories { get; init; } = new();
}

public record ShortcutCategory
{
    public string Name { get; init; } = string.Empty;
    public List<ShortcutEntry> Shortcuts { get; init; } = new();
}

public record ShortcutEntry
{
    public string Keys { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public List<string> Tags { get; init; } = new();
}
```

---

## 6. Settings DTO Pattern

WPF types like Brush or Thickness can't be serialized with System.Text.Json.
Use plain DTOs for storage and map to WPF types in the service layer.

```csharp
namespace ShortcutOverlay.Models;

/// <summary>
/// Plain DTO for JSON serialization — no WPF types allowed here.
/// </summary>
public record AppSettings
{
    public string DisplayMode { get; init; } = "floating";
    public string GlobalHotkey { get; init; } = "Ctrl+Shift+S";
    public double Opacity { get; init; } = 0.85;
    public string Theme { get; init; } = "auto";
    public string DockSide { get; init; } = "right";
    public bool StartWithWindows { get; init; } = false;
    public bool AutoSwitchProfiles { get; init; } = true;
    public bool ShowOnAllDesktops { get; init; } = true;
    public string FontSize { get; init; } = "medium";
    public PositionDto FloatingPosition { get; init; } = new(1500, 200);
    public int SidePanelWidth { get; init; } = 280;
}

public record PositionDto(double X, double Y);
```
