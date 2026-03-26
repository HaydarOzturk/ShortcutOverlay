namespace ShortcutOverlay.Helpers;

/// <summary>
/// High-level theme controller. Resolves theme settings to palettes
/// and delegates to ThemeAnimator for seamless color transitions.
/// </summary>
public static class ThemeManager
{
    private static string _currentFamily = "Classic";
    private static bool _isAdaptiveMode;
    private static bool _currentVariantIsDark = true;

    public static bool IsAdaptiveMode => _isAdaptiveMode;
    public static string CurrentFamily => _currentFamily;

    /// <summary>
    /// Initializes the theme system. Call once at startup BEFORE showing any windows.
    /// </summary>
    public static void Initialize(string themeSetting)
    {
        AdaptiveDebugLog.Enable();
        AdaptiveDebugLog.Log($"ThemeManager.Initialize called with: '{themeSetting}'");

        ParseThemeSetting(themeSetting, out var family, out var adaptive);
        _currentFamily = family;
        _isAdaptiveMode = adaptive;

        var systemTheme = GetSystemTheme();
        var isDark = systemTheme.Equals("dark", System.StringComparison.OrdinalIgnoreCase);
        _currentVariantIsDark = isDark;

        AdaptiveDebugLog.Log($"  Parsed: family={family}, adaptive={adaptive}, systemTheme={systemTheme}, isDark={isDark}");

        var palette = GetPalette(family, isDark);
        AdaptiveDebugLog.Log($"  Initial palette: {palette.Name}");
        ThemeAnimator.Initialize(palette);
    }

    /// <summary>
    /// Changes theme with smooth animation. Supports same syntax as before:
    ///   "auto", "adaptive", "adaptive:OceanTeal", "MidnightBlue", etc.
    /// </summary>
    public static void ApplyTheme(string theme)
    {
        ParseThemeSetting(theme, out var family, out var adaptive);
        _currentFamily = family;
        _isAdaptiveMode = adaptive;

        var isDark = GetSystemTheme().Equals("dark", System.StringComparison.OrdinalIgnoreCase);
        _currentVariantIsDark = isDark;

        ThemeAnimator.TransitionTo(GetPalette(family, isDark));
    }

    /// <summary>
    /// Called by adaptive mode: analyzes the foreground window's region
    /// to determine light/dark, then smoothly transitions.
    /// </summary>
    public static void AdaptToBackground(IntPtr foregroundHwnd,
        int overlayX, int overlayY, int overlayWidth, int overlayHeight)
    {
        if (!_isAdaptiveMode)
        {
            AdaptiveDebugLog.Log("AdaptToBackground: SKIP — not adaptive mode");
            return;
        }

        var isLight = ScreenBrightnessDetector.IsBackgroundLight(
            foregroundHwnd, overlayX, overlayY, overlayWidth, overlayHeight);

        // Light background → use light overlay (matches background vibe)
        // Dark background → use dark overlay
        var wantDark = !isLight;

        AdaptiveDebugLog.Log($"AdaptToBackground: isLight={isLight}, wantDark={wantDark}, currentIsDark={_currentVariantIsDark}");

        // Only transition if the variant actually needs to change
        if (wantDark == _currentVariantIsDark) return;
        _currentVariantIsDark = wantDark;

        var targetPalette = GetPalette(_currentFamily, wantDark);
        AdaptiveDebugLog.Log($"  TRANSITIONING to palette: {targetPalette.Name}");
        ThemeAnimator.TransitionTo(targetPalette);
    }

    private static ThemePalette GetPalette(string family, bool isDark)
    {
        foreach (var f in ThemePalette.Families)
        {
            if (f.Key.Equals(family, System.StringComparison.OrdinalIgnoreCase) ||
                f.DisplayName.Equals(family, System.StringComparison.OrdinalIgnoreCase))
            {
                return isDark ? f.Dark : f.Light;
            }
        }
        // Fallback to Classic
        return isDark ? ThemePalette.ClassicDark : ThemePalette.ClassicLight;
    }

    private static void ParseThemeSetting(string theme, out string family, out bool adaptive)
    {
        if (string.IsNullOrWhiteSpace(theme)) theme = "auto";

        if (theme.StartsWith("adaptive", System.StringComparison.OrdinalIgnoreCase))
        {
            adaptive = true;
            var parts = theme.Split(':');
            family = parts.Length > 1 ? parts[1].Trim() : "Classic";
            // Validate
            bool found = false;
            foreach (var f in ThemePalette.Families)
                if (f.Key.Equals(family, System.StringComparison.OrdinalIgnoreCase))
                { found = true; break; }
            if (!found) family = "Classic";
            return;
        }

        adaptive = false;

        if (theme.Equals("auto", System.StringComparison.OrdinalIgnoreCase))
        {
            family = "Classic";
            return;
        }

        // Try as family key
        foreach (var f in ThemePalette.Families)
        {
            if (f.Key.Equals(theme, System.StringComparison.OrdinalIgnoreCase) ||
                f.DisplayName.Equals(theme, System.StringComparison.OrdinalIgnoreCase))
            {
                family = f.Key;
                return;
            }
        }

        family = "Classic";
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
            return "Dark";
        }
    }
}
