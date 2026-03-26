using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using ShortcutOverlay.Models;
using ShortcutOverlay.Services;
using ShortcutOverlay.Views;

namespace ShortcutOverlay.ViewModels;

public enum DisplayMode
{
    FloatingWidget,
    SidePanel,
    TrayPopup
}

public partial class MainViewModel : ObservableObject
{
    private readonly WindowDetectionService _detection;
    private readonly ProfileManager _profileManager;
    private readonly SettingsService _settingsService;

    [ObservableProperty]
    private ShortcutProfile? currentProfile;

    [ObservableProperty]
    private string currentAppName = "No app detected";

    [ObservableProperty]
    private string searchFilter = string.Empty;

    [ObservableProperty]
    private DisplayMode currentDisplayMode = DisplayMode.FloatingWidget;

    public ObservableCollection<ShortcutCategory> FilteredCategories { get; }

    public IReadOnlyCollection<ShortcutProfile> AllProfiles => _profileManager.AllProfiles;

    /// <summary>
    /// The currently active overlay window. Set by App.xaml.cs when switching modes.
    /// </summary>
    public IOverlayMode? ActiveOverlay { get; set; }

    public MainViewModel(
        WindowDetectionService detection,
        ProfileManager profileManager,
        SettingsService settingsService)
    {
        _detection = detection;
        _profileManager = profileManager;
        _settingsService = settingsService;

        FilteredCategories = new ObservableCollection<ShortcutCategory>();

        _detection.ActiveAppChanged += OnActiveAppChanged;
    }

    private void OnActiveAppChanged(ActiveAppInfo appInfo)
    {
        CurrentAppName = string.IsNullOrEmpty(appInfo.ProcessName) ? "No app detected" : appInfo.DisplayName;

        var profile = _profileManager.GetProfileForProcess(appInfo.ProcessName);
        CurrentProfile = profile;

        ApplyFilter();
    }

    partial void OnSearchFilterChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredCategories.Clear();

        if (CurrentProfile?.Categories == null)
        {
            return;
        }

        var filter = SearchFilter?.ToLower() ?? string.Empty;

        foreach (var category in CurrentProfile.Categories.OrderBy(c => c.SortOrder))
        {
            var filteredShortcuts = new List<ShortcutEntry>();

            foreach (var shortcut in category.Shortcuts)
            {
                bool matches = string.IsNullOrEmpty(filter) ||
                    shortcut.Description.ToLower().Contains(filter) ||
                    shortcut.Keys.ToLower().Contains(filter) ||
                    (shortcut.Tags?.Any(t => t.ToLower().Contains(filter)) ?? false);

                if (matches)
                {
                    filteredShortcuts.Add(shortcut);
                }
            }

            if (filteredShortcuts.Count > 0)
            {
                var filteredCategory = new ShortcutCategory
                {
                    Name = category.Name,
                    Shortcuts = filteredShortcuts
                };
                FilteredCategories.Add(filteredCategory);
            }
        }
    }

    [RelayCommand]
    public void ClearSearch()
    {
        SearchFilter = string.Empty;
    }

    [RelayCommand]
    public void OpenSettings()
    {
        // TODO: Phase 3 — wire up settings dialog window
        System.Diagnostics.Debug.WriteLine("Settings dialog not yet implemented (Phase 3).");
    }

    [RelayCommand]
    public void ToggleOverlay()
    {
        ActiveOverlay?.ToggleVisibility();
    }
}
