using System.Windows;

namespace ShortcutOverlay.Helpers;

public static class ThemeManager
{
    public static void ApplyTheme(string theme)
    {
        var actualTheme = theme == "auto" ? GetSystemTheme() : theme;
        var themeFile = actualTheme.Equals("dark", StringComparison.OrdinalIgnoreCase)
            ? "DarkTheme" : "LightTheme";

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
