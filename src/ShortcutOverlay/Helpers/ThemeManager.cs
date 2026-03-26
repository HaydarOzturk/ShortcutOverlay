using System.Windows;

namespace ShortcutOverlay.Helpers;

public static class ThemeManager
{
    /// <summary>
    /// All available theme names (display name → XAML file name without extension).
    /// </summary>
    public static IReadOnlyList<(string DisplayName, string Key)> AvailableThemes { get; } = new List<(string, string)>
    {
        ("Dark",           "DarkTheme"),
        ("Light",          "LightTheme"),
        ("Midnight Blue",  "MidnightBlueTheme"),
        ("Rose Gold",      "RoseGoldTheme"),
        ("Ocean Teal",     "OceanTealTheme"),
        ("Forest Green",   "ForestGreenTheme"),
        ("Sunset Amber",   "SunsetAmberTheme"),
    };

    public static void ApplyTheme(string theme)
    {
        string themeFile;

        if (theme.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            themeFile = GetSystemTheme().Equals("dark", StringComparison.OrdinalIgnoreCase)
                ? "DarkTheme" : "LightTheme";
        }
        else
        {
            // Try to match by key first, then by display name
            var match = AvailableThemes.FirstOrDefault(t =>
                t.Key.Equals(theme, StringComparison.OrdinalIgnoreCase) ||
                t.DisplayName.Equals(theme, StringComparison.OrdinalIgnoreCase));

            themeFile = match.Key ?? "DarkTheme";
        }

        var uri = new Uri($"Resources/Styles/{themeFile}.xaml", UriKind.Relative);
        var dict = new ResourceDictionary { Source = uri };

        var merged = Application.Current.Resources.MergedDictionaries;
        // Index 0 = BaseStyles, Index 1 = Theme
        if (merged.Count > 1)
            merged[1] = dict;
        else
            merged.Add(dict);
    }

    private static string GetSystemTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int v && v == 0 ? "Dark" : "Light";
        }
        catch
        {
            return "Dark"; // Default to dark if registry read fails
        }
    }
}
