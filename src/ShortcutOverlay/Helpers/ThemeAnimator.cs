using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ShortcutOverlay.Helpers;

/// <summary>
/// Owns a single set of mutable SolidColorBrush resources registered in Application.Resources.
/// Smoothly animates between ThemePalettes using ColorAnimation — no ResourceDictionary swaps.
/// This is the secret to seamless, invisible transitions.
/// </summary>
public static class ThemeAnimator
{
    // Animation duration — 200ms with cubic ease feels Apple-like
    private static readonly Duration AnimDuration = new(TimeSpan.FromMilliseconds(200));
    private static readonly IEasingFunction Easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

    // All resource keys we manage
    private static readonly string[] BrushKeys =
    {
        "OverlayBackground", "OverlayBorder", "HeaderBackground",
        "PrimaryText", "SecondaryText", "TertiaryText",
        "KeyBadgeBackground", "KeyBadgeBorder", "KeyBadgeText",
        "SearchBackground", "SearchText", "SearchPlaceholder", "SearchBorder",
        "ShortcutRowBackground", "ShortcutRowHover", "ShortcutRowBorder",
        "CategoryDivider",
        "FooterButtonBackground", "FooterButtonHover", "FooterButtonText",
        "AccentColor", "AccentColorSubtle",
        "ScrollbarThumb", "ScrollbarTrack",
    };

    private static ThemePalette? _currentPalette;
    private static bool _initialized;

    /// <summary>
    /// Creates mutable SolidColorBrush resources in Application.Resources.
    /// Must be called once at startup BEFORE any UI references DynamicResource keys.
    /// </summary>
    public static void Initialize(ThemePalette initialPalette)
    {
        var res = Application.Current.Resources;

        // Remove any existing theme ResourceDictionary (index 1 from App.xaml)
        // We'll replace it with direct brush entries
        var merged = res.MergedDictionaries;
        if (merged.Count > 1)
            merged.RemoveAt(1); // Remove the DarkTheme.xaml default

        // Create mutable brushes with initial colors
        foreach (var key in BrushKeys)
        {
            var color = GetColor(initialPalette, key);
            var brush = new SolidColorBrush(color);
            // Do NOT freeze — we need to animate these
            res[key] = brush;
        }

        _currentPalette = initialPalette;
        _initialized = true;
    }

    /// <summary>
    /// Smoothly transitions all brush colors to the target palette.
    /// If already at this palette, does nothing. Animation is ~200ms.
    /// </summary>
    public static void TransitionTo(ThemePalette target)
    {
        if (!_initialized) return;
        if (_currentPalette?.Name == target.Name) return;

        var res = Application.Current.Resources;

        foreach (var key in BrushKeys)
        {
            if (res[key] is SolidColorBrush brush)
            {
                var toColor = GetColor(target, key);
                var anim = new ColorAnimation
                {
                    To = toColor,
                    Duration = AnimDuration,
                    EasingFunction = Easing,
                    FillBehavior = FillBehavior.HoldEnd,
                };
                brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
            }
        }

        _currentPalette = target;
    }

    /// <summary>
    /// Instantly sets all brush colors with no animation (for initial load).
    /// </summary>
    public static void SetImmediate(ThemePalette target)
    {
        if (!_initialized) return;
        var res = Application.Current.Resources;

        foreach (var key in BrushKeys)
        {
            if (res[key] is SolidColorBrush brush)
            {
                // Remove any running animation first
                brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
                brush.Color = GetColor(target, key);
            }
        }

        _currentPalette = target;
    }

    /// <summary>
    /// Extracts a Color from a ThemePalette by resource key name.
    /// Uses reflection-free switch for performance.
    /// </summary>
    private static Color GetColor(ThemePalette p, string key) => key switch
    {
        "OverlayBackground" => p.OverlayBackground,
        "OverlayBorder" => p.OverlayBorder,
        "HeaderBackground" => p.HeaderBackground,
        "PrimaryText" => p.PrimaryText,
        "SecondaryText" => p.SecondaryText,
        "TertiaryText" => p.TertiaryText,
        "KeyBadgeBackground" => p.KeyBadgeBackground,
        "KeyBadgeBorder" => p.KeyBadgeBorder,
        "KeyBadgeText" => p.KeyBadgeText,
        "SearchBackground" => p.SearchBackground,
        "SearchText" => p.SearchText,
        "SearchPlaceholder" => p.SearchPlaceholder,
        "SearchBorder" => p.SearchBorder,
        "ShortcutRowBackground" => p.ShortcutRowBackground,
        "ShortcutRowHover" => p.ShortcutRowHover,
        "ShortcutRowBorder" => p.ShortcutRowBorder,
        "CategoryDivider" => p.CategoryDivider,
        "FooterButtonBackground" => p.FooterButtonBackground,
        "FooterButtonHover" => p.FooterButtonHover,
        "FooterButtonText" => p.FooterButtonText,
        "AccentColor" => p.AccentColor,
        "AccentColorSubtle" => p.AccentColorSubtle,
        "ScrollbarThumb" => p.ScrollbarThumb,
        "ScrollbarTrack" => p.ScrollbarTrack,
        _ => Colors.Transparent,
    };
}
