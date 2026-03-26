using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace ShortcutOverlay.Helpers;

/// <summary>
/// Owns theme color resources in Application.Resources.
/// Smoothly transitions between ThemePalettes using manual timer-based interpolation.
///
/// WPF automatically FREEZES brushes added to Application.Resources, making
/// ColorAnimation impossible (throws InvalidOperationException). Instead, we
/// interpolate colors manually at 60fps using a DispatcherTimer and replace
/// the frozen brush with a new one each frame. DynamicResource bindings in XAML
/// automatically pick up the replacement, so this works seamlessly.
///
/// The visual result is identical to ColorAnimation: smooth 200ms transitions.
/// </summary>
public static class ThemeAnimator
{
    // Transition config
    private const int NormalTransitionMs = 200;
    private const int FastTransitionMs = 80;   // For adaptive mode — snappier
    private const int FrameIntervalMs = 16;    // ~60fps
    private static int _totalFrames = NormalTransitionMs / FrameIntervalMs;

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

    // Transition state
    private static DispatcherTimer? _transitionTimer;
    private static ThemePalette? _fromPalette;
    private static ThemePalette? _toPalette;
    private static int _currentFrame;

    /// <summary>
    /// Creates initial brush resources in Application.Resources.
    /// Must be called once at startup BEFORE any UI references DynamicResource keys.
    /// </summary>
    public static void Initialize(ThemePalette initialPalette)
    {
        var res = Application.Current.Resources;

        // Remove any existing theme ResourceDictionary (index 1 from App.xaml)
        var merged = res.MergedDictionaries;
        if (merged.Count > 1)
            merged.RemoveAt(1); // Remove the DarkTheme.xaml default

        // Create brushes with initial colors
        foreach (var key in BrushKeys)
        {
            var color = GetColor(initialPalette, key);
            res[key] = new SolidColorBrush(color);
        }

        // Set up the transition timer (created once, started/stopped per transition)
        _transitionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(FrameIntervalMs)
        };
        _transitionTimer.Tick += OnTransitionFrame;

        _currentPalette = initialPalette;
        _initialized = true;

        AdaptiveDebugLog.Log($"ThemeAnimator.Initialize: palette={initialPalette.Name}, {BrushKeys.Length} brushes created");
    }

    /// <summary>
    /// Smoothly transitions all brush colors to the target palette over ~200ms.
    /// </summary>
    public static void TransitionTo(ThemePalette target)
    {
        StartTransition(target, NormalTransitionMs);
    }

    /// <summary>
    /// Fast transition (~80ms) for adaptive mode — snappy but not jarring.
    /// </summary>
    public static void TransitionFast(ThemePalette target)
    {
        StartTransition(target, FastTransitionMs);
    }

    private static void StartTransition(ThemePalette target, int durationMs)
    {
        if (!_initialized) return;
        if (_currentPalette?.Name == target.Name) return;

        _transitionTimer?.Stop();

        _fromPalette = _currentPalette;
        _toPalette = target;
        _currentFrame = 0;
        _totalFrames = Math.Max(1, durationMs / FrameIntervalMs);
        _currentPalette = target;

        AdaptiveDebugLog.Log($"ThemeAnimator.TransitionTo: {_fromPalette?.Name} → {target.Name} ({durationMs}ms, {_totalFrames} frames)");

        _transitionTimer?.Start();
    }

    /// <summary>
    /// Called ~60 times per second during a transition. Interpolates colors and
    /// replaces the brush resources. DynamicResource bindings update automatically.
    /// </summary>
    private static void OnTransitionFrame(object? sender, EventArgs e)
    {
        if (_fromPalette == null || _toPalette == null || _transitionTimer == null)
        {
            _transitionTimer?.Stop();
            return;
        }

        _currentFrame++;
        double t = Math.Min(1.0, (double)_currentFrame / _totalFrames);

        // Apply cubic ease-in-out for smooth acceleration/deceleration
        double eased = CubicEaseInOut(t);

        var res = Application.Current.Resources;
        foreach (var key in BrushKeys)
        {
            var fromColor = GetColor(_fromPalette, key);
            var toColor = GetColor(_toPalette, key);
            var current = LerpColor(fromColor, toColor, eased);
            res[key] = new SolidColorBrush(current);
        }

        // Done?
        if (t >= 1.0)
        {
            _transitionTimer.Stop();
            _fromPalette = null;
            AdaptiveDebugLog.Log($"ThemeAnimator: Transition complete ({_currentFrame} frames)");
        }
    }

    /// <summary>
    /// Instantly sets all brush colors with no animation (for cached app switches).
    /// </summary>
    public static void SetImmediate(ThemePalette target)
    {
        if (!_initialized) return;
        _transitionTimer?.Stop();

        var res = Application.Current.Resources;
        foreach (var key in BrushKeys)
        {
            res[key] = new SolidColorBrush(GetColor(target, key));
        }

        _currentPalette = target;
    }

    /// <summary>
    /// Boosts overlay background opacity when background content is mixed (high variance).
    /// This improves readability when the overlay sits over text or split-tone content.
    /// </summary>
    public static void AdjustForReadability(double variance)
    {
        if (!_initialized) return;

        // variance > 0.02 means moderately mixed, > 0.05 means very mixed
        if (variance < 0.015) return; // Background is uniform — no boost needed

        var res = Application.Current.Resources;
        if (res["OverlayBackground"] is SolidColorBrush currentBrush)
        {
            var c = currentBrush.Color;
            // Boost alpha: map variance 0.015..0.08 → alpha increase of 0..80
            double boost = Math.Min(80, (variance - 0.015) / 0.065 * 80);
            byte newAlpha = (byte)Math.Min(255, c.A + boost);

            if (newAlpha != c.A)
            {
                res["OverlayBackground"] = new SolidColorBrush(
                    Color.FromArgb(newAlpha, c.R, c.G, c.B));
                AdaptiveDebugLog.Log($"Readability boost: variance={variance:F3}, alpha {c.A}→{newAlpha}");
            }
        }
    }

    /// <summary>
    /// Linear interpolation between two colors.
    /// </summary>
    private static Color LerpColor(Color a, Color b, double t)
    {
        return Color.FromArgb(
            (byte)(a.A + (b.A - a.A) * t),
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    /// <summary>
    /// Cubic ease-in-out: smooth start + smooth end.
    /// </summary>
    private static double CubicEaseInOut(double t)
    {
        return t < 0.5
            ? 4 * t * t * t
            : 1 - Math.Pow(-2 * t + 2, 3) / 2;
    }

    /// <summary>
    /// Extracts a Color from a ThemePalette by resource key name.
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
