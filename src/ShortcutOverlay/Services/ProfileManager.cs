using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShortcutOverlay.Models;

namespace ShortcutOverlay.Services;

/// <summary>
/// Manages loading and persisting shortcut profiles.
/// On first run, copies embedded default profiles from Resources/DefaultProfiles to AppData.
/// Maps process names and window classes to profiles using case-insensitive lookups.
/// </summary>
public sealed class ProfileManager
{
    private readonly string _profilesPath;
    private readonly Dictionary<string, ShortcutProfile> _profilesByProcessName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ShortcutProfile> _profilesByWindowClass = new(StringComparer.OrdinalIgnoreCase);
    private readonly JsonSerializerOptions _jsonOptions;

    public IReadOnlyCollection<ShortcutProfile> AllProfiles => _profilesByProcessName.Values.Distinct().ToList();

    public ProfileManager()
    {
        _profilesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ShortcutOverlay",
            "profiles");

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        };

        EnsureDefaultProfiles();
        LoadProfiles();
    }

    /// <summary>
    /// Ensures default profiles exist in AppData. Copies from embedded resources on first run.
    /// </summary>
    private void EnsureDefaultProfiles()
    {
        Directory.CreateDirectory(_profilesPath);

        // Check if profiles already exist
        var existingProfiles = Directory.GetFiles(_profilesPath, "*.json");
        if (existingProfiles.Length > 0)
            return;

        // Copy default profiles from Resources/DefaultProfiles
        var resourcePath = GetResourceProfilePath();
        if (!Directory.Exists(resourcePath))
            return;

        foreach (var file in Directory.GetFiles(resourcePath, "*.json"))
        {
            var fileName = Path.GetFileName(file);
            var destPath = Path.Combine(_profilesPath, fileName);
            File.Copy(file, destPath, overwrite: false);
        }
    }

    /// <summary>
    /// Gets the path to the embedded default profiles in Resources.
    /// </summary>
    private static string GetResourceProfilePath()
    {
        var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var assemblyDir = Path.GetDirectoryName(assemblyLocation) ?? string.Empty;
        return Path.Combine(assemblyDir, "Resources", "DefaultProfiles");
    }

    /// <summary>
    /// Loads all profiles from the AppData folder and indexes them by process name and window class.
    /// </summary>
    private void LoadProfiles()
    {
        _profilesByProcessName.Clear();
        _profilesByWindowClass.Clear();

        if (!Directory.Exists(_profilesPath))
            return;

        foreach (var filePath in Directory.GetFiles(_profilesPath, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var profile = JsonSerializer.Deserialize<ShortcutProfile>(json, _jsonOptions);

                if (profile == null || string.IsNullOrEmpty(profile.ProfileId))
                    continue;

                // Index by process names
                foreach (var processName in profile.ProcessNames ?? new List<string>())
                {
                    if (!string.IsNullOrEmpty(processName))
                        _profilesByProcessName[processName] = profile;
                }

                // Index by window classes
                foreach (var windowClass in profile.WindowClasses ?? new List<string>())
                {
                    if (!string.IsNullOrEmpty(windowClass))
                        _profilesByWindowClass[windowClass] = profile;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load profile from {filePath}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Gets the profile for a given process name. Uses case-insensitive lookup.
    /// Returns null if no matching profile is found.
    /// </summary>
    public ShortcutProfile? GetProfileForProcess(string processName)
    {
        if (string.IsNullOrEmpty(processName))
            return null;

        // Try process name first (case-insensitive)
        if (_profilesByProcessName.TryGetValue(processName, out var profile))
            return profile;

        // Fall back to window class lookup if needed
        if (_profilesByWindowClass.TryGetValue(processName, out var classProfile))
            return classProfile;

        return null;
    }

    /// <summary>
    /// Gets the profile by window class name. Uses case-insensitive lookup.
    /// </summary>
    public ShortcutProfile? GetProfileByWindowClass(string windowClass)
    {
        if (string.IsNullOrEmpty(windowClass))
            return null;

        _profilesByWindowClass.TryGetValue(windowClass, out var profile);
        return profile;
    }

    /// <summary>
    /// Saves a profile to disk and updates the in-memory index.
    /// </summary>
    public async Task SaveProfileAsync(ShortcutProfile profile)
    {
        if (profile == null || string.IsNullOrEmpty(profile.ProfileId))
            throw new ArgumentException("Profile must have a valid ProfileId");

        Directory.CreateDirectory(_profilesPath);

        var filePath = Path.Combine(_profilesPath, $"{profile.ProfileId}.json");
        var json = JsonSerializer.Serialize(profile, _jsonOptions);

        await File.WriteAllTextAsync(filePath, json);

        // Update in-memory index
        foreach (var processName in profile.ProcessNames ?? new List<string>())
        {
            if (!string.IsNullOrEmpty(processName))
                _profilesByProcessName[processName] = profile;
        }

        foreach (var windowClass in profile.WindowClasses ?? new List<string>())
        {
            if (!string.IsNullOrEmpty(windowClass))
                _profilesByWindowClass[windowClass] = profile;
        }
    }

    /// <summary>
    /// Reloads all profiles from disk.
    /// </summary>
    public void Reload()
    {
        LoadProfiles();
    }
}
