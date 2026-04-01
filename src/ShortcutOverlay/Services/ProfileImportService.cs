using System.IO;
using System.Net.Http;
using System.Text.Json;
using ShortcutOverlay.Models;

namespace ShortcutOverlay.Services;

/// <summary>
/// Imports shortcut profiles from URLs or local files.
/// Validates JSON against ShortcutProfile schema before saving.
/// </summary>
public static class ProfileImportService
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Downloads and imports a profile from a URL.
    /// Returns (success, message).
    /// </summary>
    public static async Task<(bool Success, string Message)> ImportFromUrlAsync(string url)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return (false, "Invalid URL format.");

            var json = await _httpClient.GetStringAsync(uri);
            return await ImportFromJsonAsync(json);
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Download failed: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return (false, "Download timed out.");
        }
        catch (Exception ex)
        {
            return (false, $"Import failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Imports a profile from a local JSON file.
    /// </summary>
    public static async Task<(bool Success, string Message)> ImportFromFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return (false, "File not found.");

            var json = await File.ReadAllTextAsync(filePath);
            return await ImportFromJsonAsync(json);
        }
        catch (Exception ex)
        {
            return (false, $"Import failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates and saves a JSON profile string.
    /// </summary>
    private static async Task<(bool Success, string Message)> ImportFromJsonAsync(string json)
    {
        var profile = JsonSerializer.Deserialize<ShortcutProfile>(json, _jsonOptions);

        if (profile == null)
            return (false, "Invalid profile JSON — could not deserialize.");

        if (string.IsNullOrWhiteSpace(profile.ProfileId))
            return (false, "Profile is missing a ProfileId.");

        if (profile.Categories.Count == 0)
            return (false, "Profile has no shortcut categories.");

        // Save to profiles folder
        var profilesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ShortcutOverlay", "profiles");

        Directory.CreateDirectory(profilesPath);

        var targetFile = Path.Combine(profilesPath, $"{profile.ProfileId}.json");
        await File.WriteAllTextAsync(targetFile, json);

        return (true, $"Imported '{profile.DisplayName}' ({profile.Categories.Count} categories, {profile.Categories.Sum(c => c.Shortcuts.Count)} shortcuts).");
    }
}
