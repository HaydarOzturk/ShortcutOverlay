using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShortcutOverlay.Models;
using ShortcutOverlay.Services;

namespace ShortcutOverlay.ViewModels;

public partial class ShortcutEditorViewModel : ObservableObject
{
    private readonly string _profilesPath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    [ObservableProperty]
    private ShortcutProfile? selectedProfile;

    [ObservableProperty]
    private ShortcutCategory? selectedCategory;

    [ObservableProperty]
    private string importUrl = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public ObservableCollection<ShortcutProfile> Profiles { get; } = new();

    public ShortcutEditorViewModel()
    {
        _profilesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ShortcutOverlay", "profiles");

        LoadProfiles();
    }

    private void LoadProfiles()
    {
        Profiles.Clear();
        if (!Directory.Exists(_profilesPath)) return;

        foreach (var file in Directory.GetFiles(_profilesPath, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var profile = JsonSerializer.Deserialize<ShortcutProfile>(json, _jsonOptions);
                if (profile != null)
                    Profiles.Add(profile);
            }
            catch { /* Skip corrupt files */ }
        }

        if (Profiles.Count > 0)
            SelectedProfile = Profiles[0];
    }

    partial void OnSelectedProfileChanged(ShortcutProfile? value)
    {
        SelectedCategory = value?.Categories.FirstOrDefault();
    }

    [RelayCommand]
    public void AddCategory()
    {
        if (SelectedProfile == null) return;

        var newCategory = new ShortcutCategory
        {
            Name = $"New Category {SelectedProfile.Categories.Count + 1}",
            Shortcuts = new List<ShortcutEntry>()
        };

        SelectedProfile.Categories.Add(newCategory);
        SelectedCategory = newCategory;
        StatusMessage = "Category added. Don't forget to save.";
    }

    [RelayCommand]
    public void AddShortcut()
    {
        if (SelectedCategory == null) return;

        var entry = new ShortcutEntry
        {
            Keys = "Ctrl+",
            Description = "New shortcut"
        };

        SelectedCategory.Shortcuts.Add(entry);
        StatusMessage = "Shortcut added. Don't forget to save.";
    }

    [RelayCommand]
    public async Task SaveProfileAsync()
    {
        if (SelectedProfile == null) return;

        try
        {
            Directory.CreateDirectory(_profilesPath);
            var filePath = Path.Combine(_profilesPath, $"{SelectedProfile.ProfileId}.json");
            var json = JsonSerializer.Serialize(SelectedProfile, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
            StatusMessage = $"Saved '{SelectedProfile.DisplayName}'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ImportFromUrlAsync()
    {
        if (string.IsNullOrWhiteSpace(ImportUrl))
        {
            StatusMessage = "Enter a URL first.";
            return;
        }

        StatusMessage = "Importing...";
        var (success, message) = await ProfileImportService.ImportFromUrlAsync(ImportUrl);
        StatusMessage = message;

        if (success)
        {
            LoadProfiles(); // Reload to show imported profile
            ImportUrl = string.Empty;
        }
    }

    [RelayCommand]
    public async Task ImportFromFileAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title = "Import Shortcut Profile"
        };

        if (dialog.ShowDialog() == true)
        {
            StatusMessage = "Importing...";
            var (success, message) = await ProfileImportService.ImportFromFileAsync(dialog.FileName);
            StatusMessage = message;

            if (success)
                LoadProfiles();
        }
    }
}
