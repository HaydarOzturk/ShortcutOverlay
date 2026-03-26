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
/// Anti-flicker: readability alpha boost is baked into the transition target
/// rather than applied as a post-hoc adjustment that fights with the timer.
/// A cooldown prevents rapid-fire transitions from causing visible flicker.
/// </summary>
public static class ThemeAnimator
{
    // Transition config
    private const int NormalTransitionMs = 200;
    private const int FastTransitionMs = 100;   // For adaptive mode — snappy but smooth
    private const int FrameIntervalMs = 16;     // ~60fps
    private static int _totalFrames = NormalTransitionMs / FrameIntervalMs;

    // Anti-flicker: minimum time between transitions (ms)
    private const int TransitionCooldownMs = 300;
    private static DateTime _lastTransitionStart = DateTime.MinValue;

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
    private static bool _isTransitioning;

    // Readability: current alpha boost applied to OverlayBackground
    private static byte _currentAlphaBoost;
    private static byte _targetAlphaBoost;

    // Transition state
    private static DispatcherTimer? _transitionTimer;
    private static ThemePalette? _fromPalette;
    private static ThemePalette? _toPalette;
    private static int _currentFrame;

    /// <summary>Whether a transition is currently in progress.</summary>
    public static bool IsTransitioning => _isTransitioning;

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
    /// Fast transition (~100ms) for adaptive mode — snappy but not jarring.
    /// </summary>
    public static void TransitionFast(ThemePalette target)
    {
        StartTransition(target, FastTransitionMs);
    }

    private static void StartTransition(ThemePalette target, int durationMs)
    {
        if (!_initialized) return;
        if (_currentPalette?.Name == target.Name) return;

        // Anti-flicker cooldown: reject transitions that come too fast
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastTransitionStart).TotalMilliseconds;
        if (elapsed < TransitionCooldownMs && _isTransitioning)
        {
            AdaptiveDebugLog.Log($"ThemeAnimator: COOLDOWN — skipping transition ({elapsed:F0}ms since last)");
            return;
        }

        // If we're mid-transition, snapshot the actual on-screen colors
        // so the new transition starts from where we visually ARE
        var wasTransitioning = _isTransitioning;

        _transitionTimer?.Stop();
        _isTransitioning = true;
        _lastTransitionStart = now;

        _fromPalette = wasTransitioning ? SnapshotCurrentColors() : _currentPalette;
        _toPalette = target;
        _currentFrame = 0;
        _totalFrames = Math.Max(1, durationMs / FrameIntervalMs);
        _currentPalette = target;

        AdaptiveDebugLog.Log($"ThemeAnimator.TransitionTo: → {target.Name} ({durationMs}ms, {_totalFrames} frames, snapshot={wasTransitioning})");

        _transitionTimer?.Start();
    }

    /// <summary>
    /// Captures the current on-screen colors as a snapshot palette.
    /// Used when a new transition starts mid-way through a previous one,
    /// so we interpolate from where we actually ARE, not where we were headed.
    /// </summary>
    private static ThemePalette SnapshotCurrentColors()
    {
        var res = Application.Current.Resources;
        Color Snap(string key)
        {
            if (res[key] is SolidColorBrush brush)
                return brush.Color;
            return _currentPalette != null ? GetColor(_currentPalette, key) : Colors.Transparent;
        }

        return new ThemePalette
        {
            Name = "_snapshot_",
            OverlayBackground = Snap("OverlayBackground"),
            OverlayBorder = Snap("OverlayBorder"),
            HeaderBackground = Snap("HeaderBackground"),
            PrimaryText = Snap("PrimaryText"),
            SecondaryText = Snap("SecondaryText"),
            TertiaryText = Snap("TertiaryText"),
            KeyBadgeBackground = Snap("KeyBadgeBackground"),
            KeyBadgeBorder = Snap("KeyBadgeBorder"),
            KeyBadgeText = Snap("KeyBadgeText"),
            SearchBackground = Snap("SearchBackground"),
            SearchText = Snap("SearchText"),
            SearchPlaceholder = Snap("SearchPlaceholder"),
            SearchBorder = Snap("SearchBorder"),
            ShortcutRowBackground = Snap("ShortcutRowBackground"),
            ShortcutRowHover = Snap("ShortcutRowHover"),
            ShortcutRowBorder = Snap("ShortcutRowBorder"),
            CategoryDivider = Snap("CategoryDivider"),
            FooterButtonBackground = Snap("FooterButtonBackground"),
            FooterButtonHover = Snap("FooterButtonHover"),
            FooterButtonText = Snap("FooterButtonText"),
            AccentColor = Snap("AccentColor"),
            AccentColorSubtle = Snap("AccentColorSubtle"),
            ScrollbarThumb = Snap("ScrollbarThumb"),
            ScrollbarTrack = Snap("ScrollbarTrack"),
        };
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
            _isTransitioning = false;
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

            // Bake readability boost into OverlayBackground during transition
            if (key == "OverlayBackground" && _targetAlphaBoost > 0)
            {
                byte boostedAlpha = (byte)Math.Min(255, current.A + _targetAlphaBoost * eased);
                current = Color.FromArgb(boostedAlpha, current.R, current.G, current.B);
            }

            res[key] = new SolidColorBrush(current);
        }

        // Done?
        if (t >= 1.0)
        {
            _transitionTimer.Stop();
            _isTransitioning = false;
            _fromPalette = null;
            _currentAlphaBoost = _targetAlphaBoost;
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
        _isTransitioning = false;

        var res = Application.Current.Resources;
        foreach (var key in BrushKeys)
        {
            var color = GetColor(target, key);

            // Apply current readability boost to OverlayBackground
            if (key == "OverlayBackground" && _currentAlphaBoost > 0)
            {
                color = Color.FromArgb(
                    (byte)Math.Min(255, color.A + _currentAlphaBoost),
                    color.R, color.G, color.B);
            }

            res[key] = new SolidColorBrush(color);
        }

        _currentPalette = target;
        _lastTransitionStart = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets the readability alpha boost based on background variance.
    /// Instead of immediately replacing the brush (which causes flicker),
    /// this stores the target boost and smoothly applies it on the next
    /// transition frame or SetImmediate call.
    ///
    /// If no transition is active, applies the boost directly but only
    /// if the change is significant enough to warrant a visual update.
    /// </summary>
    public static void AdjustForReadability(double variance)
    {
        if (!_initialized) return;

        // Calculate target boost: variance 0.015..0.08 → alpha increase 0..60
        byte newBoost = 0;
        if (variance >= 0.015)
            newBoost = (byte)Math.Min(60, (variance - 0.015) / 0.065 * 60);

        _targetAlphaBoost = newBoost;

        // If we're mid-transition, the boost is baked into OnTransitionFrame — don't touch brushes
        if (_isTransitioning) return;

        // Only apply if change is significant (>10 units) to avoid micro-flicker
        if (Math.Abs(newBoost - _currentAlphaBoost) < 10) return;

        _currentAlphaBoost = newBoost;

        // Apply to current OverlayBackground
        var res = Application.Current.Resources;
        if (_currentPalette != null)
        {
            var baseColor = GetColor(_currentPalette, "OverlayBackground");
            byte finalAlpha = (byte)Math.Min(255, baseColor.A + newBoost);
            res["OverlayBackground"] = new SolidColorBrush(
                Color.FromArgb(finalAlpha, baseColor.R, baseColor.G, baseColor.B));
            AdaptiveDebugLog.Log($"Readability: variance={variance:F3}, boost={newBoost}, alpha→{finalAlpha}");
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
