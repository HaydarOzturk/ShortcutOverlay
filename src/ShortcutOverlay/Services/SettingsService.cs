using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShortcutOverlay.Models;

namespace ShortcutOverlay.Services;

/// <summary>
/// Persists application settings as JSON to AppData/ShortcutOverlay/settings.json.
/// Provides singleton access via the Current property.
/// Creates default settings if the file doesn't exist.
/// </summary>
public sealed class SettingsService
{
    private static SettingsService? _instance;
    private static readonly object _instanceLock = new();

    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions;
    private AppSettings _currentSettings;

    public static SettingsService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    _instance ??= new SettingsService();
                }
            }
            return _instance;
        }
    }

    public AppSettings Current
    {
        get => _currentSettings;
    }

    public SettingsService()
    {
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ShortcutOverlay",
            "settings.json");

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        };

        _currentSettings = new AppSettings();
    }

    /// <summary>
    /// Loads settings synchronously from disk. Safe to call on UI thread at startup.
    /// Creates default settings if file doesn't exist.
    /// </summary>
    public void Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
                if (loaded != null)
                {
                    _currentSettings = loaded;
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
        }

        // Create default settings
        _currentSettings = new AppSettings();
        Save();
    }

    /// <summary>
    /// Loads settings from disk. Creates default settings if file doesn't exist.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = await File.ReadAllTextAsync(_settingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
                if (loaded != null)
                {
                    _currentSettings = loaded;
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
        }

        // Create default settings
        _currentSettings = new AppSettings();
        await SaveAsync();
    }

    /// <summary>
    /// Saves the current settings to disk synchronously.
    /// </summary>
    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(_currentSettings, _jsonOptions);
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves the current settings to disk.
    /// </summary>
    public async Task SaveAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(_currentSettings, _jsonOptions);
            await File.WriteAllTextAsync(_settingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates settings and persists to disk.
    /// </summary>
    public async Task UpdateAsync(AppSettings newSettings)
    {
        if (newSettings != null)
        {
            _currentSettings = newSettings;
            await SaveAsync();
        }
    }
}
