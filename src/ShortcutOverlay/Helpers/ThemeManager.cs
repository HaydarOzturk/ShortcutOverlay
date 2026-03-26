using System.Collections.Concurrent;
using System.Diagnostics;

namespace ShortcutOverlay.Helpers;

/// <summary>
/// High-level theme controller with per-app brightness caching.
///
/// When switching to a KNOWN app, applies the cached theme INSTANTLY (0ms).
/// Unknown apps get a fast 80ms transition after brightness detection.
/// Background polling verifies and updates the cache every 1.5s.
/// </summary>
public static class ThemeManager
{
    private static string _currentFamily = "Classic";
    private static bool _isAdaptiveMode;
    private static bool _currentVariantIsDark = true;

    // Per-app brightness cache: process name → isDark
    // This is the key to instant switching — we remember what theme each app needs
    private static readonly ConcurrentDictionary<string, bool> _appBrightnessCache = new();

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
    /// Changes theme with smooth animation.
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
    /// Called by adaptive mode. Uses per-app cache for instant switching on known apps.
    /// Falls back to brightness detection for unknown apps.
    /// </summary>
    public static void AdaptToBackground(IntPtr foregroundHwnd,
        int overlayX, int overlayY, int overlayWidth, int overlayHeight)
    {
        if (!_isAdaptiveMode) return;

        // Get process name for caching
        var processName = GetProcessName(foregroundHwnd);

        // Check cache first — instant apply for known apps
        if (!string.IsNullOrEmpty(processName) && _appBrightnessCache.TryGetValue(processName, out bool cachedIsDark))
        {
            if (cachedIsDark != _currentVariantIsDark)
            {
                AdaptiveDebugLog.Log($"AdaptToBackground: CACHED '{processName}' isDark={cachedIsDark} — INSTANT switch");
                _currentVariantIsDark = cachedIsDark;
                ThemeAnimator.SetImmediate(GetPalette(_currentFamily, cachedIsDark));
            }

            // Still run detection in background to verify and update cache
            var isLight = ScreenBrightnessDetector.IsBackgroundLight(
                foregroundHwnd, overlayX, overlayY, overlayWidth, overlayHeight);
            var freshIsDark = !isLight;

            if (freshIsDark != cachedIsDark)
            {
                // Cache was wrong (app changed theme, window resized, etc.)
                AdaptiveDebugLog.Log($"AdaptToBackground: Cache STALE for '{processName}': was={cachedIsDark}, now={freshIsDark}");
                _appBrightnessCache[processName] = freshIsDark;
                if (freshIsDark != _currentVariantIsDark)
                {
                    _currentVariantIsDark = freshIsDark;
                    ThemeAnimator.TransitionFast(GetPalette(_currentFamily, freshIsDark));
                }
            }

            // Adjust readability based on current background variance
            ThemeAnimator.AdjustForReadability(ScreenBrightnessDetector.LastBrightnessVariance);
            return;
        }

        // Unknown app — detect brightness, cache it, transition fast
        var detected = ScreenBrightnessDetector.IsBackgroundLight(
            foregroundHwnd, overlayX, overlayY, overlayWidth, overlayHeight);
        var wantDark = !detected;

        // Cache this result
        if (!string.IsNullOrEmpty(processName))
        {
            _appBrightnessCache[processName] = wantDark;
            AdaptiveDebugLog.Log($"AdaptToBackground: NEW '{processName}' isLight={detected}, cached isDark={wantDark}");
        }

        if (wantDark == _currentVariantIsDark)
        {
            // Same theme, but still adjust readability based on background variance
            ThemeAnimator.AdjustForReadability(ScreenBrightnessDetector.LastBrightnessVariance);
            return;
        }
        _currentVariantIsDark = wantDark;

        var targetPalette = GetPalette(_currentFamily, wantDark);
        AdaptiveDebugLog.Log($"  TRANSITIONING (fast) to palette: {targetPalette.Name}");
        ThemeAnimator.TransitionFast(targetPalette);

        // Adjust readability after applying new palette
        ThemeAnimator.AdjustForReadability(ScreenBrightnessDetector.LastBrightnessVariance);
    }

    /// <summary>
    /// Gets the process name from a window handle.
    /// </summary>
    private static string GetProcessName(IntPtr hwnd)
    {
        try
        {
            if (hwnd == IntPtr.Zero) return string.Empty;
            NativeInterop.Win32Api.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0) return string.Empty;
            using var proc = Process.GetProcessById((int)pid);
            return proc.ProcessName.ToLowerInvariant();
        }
        catch
        {
            return string.Empty;
        }
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
