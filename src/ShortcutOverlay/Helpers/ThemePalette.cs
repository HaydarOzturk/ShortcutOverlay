using System.Windows.Media;

namespace ShortcutOverlay.Helpers;

/// <summary>
/// A complete set of colors defining a theme variant.
/// These are plain data — no XAML, no ResourceDictionary.
/// ThemeAnimator interpolates between palettes using ColorAnimation.
/// </summary>
public class ThemePalette
{
    public required string Name { get; init; }

    public required Color OverlayBackground { get; init; }
    public required Color OverlayBorder { get; init; }
    public required Color HeaderBackground { get; init; }
    public required Color PrimaryText { get; init; }
    public required Color SecondaryText { get; init; }
    public required Color TertiaryText { get; init; }
    public required Color KeyBadgeBackground { get; init; }
    public required Color KeyBadgeBorder { get; init; }
    public required Color KeyBadgeText { get; init; }
    public required Color SearchBackground { get; init; }
    public required Color SearchText { get; init; }
    public required Color SearchPlaceholder { get; init; }
    public required Color SearchBorder { get; init; }
    public required Color ShortcutRowBackground { get; init; }
    public required Color ShortcutRowHover { get; init; }
    public required Color ShortcutRowBorder { get; init; }
    public required Color CategoryDivider { get; init; }
    public required Color FooterButtonBackground { get; init; }
    public required Color FooterButtonHover { get; init; }
    public required Color FooterButtonText { get; init; }
    public required Color AccentColor { get; init; }
    public required Color AccentColorSubtle { get; init; }
    public required Color ScrollbarThumb { get; init; }
    public required Color ScrollbarTrack { get; init; }

    private static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    // ── Classic ──
    public static ThemePalette ClassicDark => new()
    {
        Name = "ClassicDark",
        OverlayBackground = C("#C01E1E1E"), OverlayBorder = C("#18FFFFFF"),
        HeaderBackground = C("#0AFFFFFF"),
        PrimaryText = C("#F0FFFFFF"), SecondaryText = C("#80FFFFFF"), TertiaryText = C("#50FFFFFF"),
        KeyBadgeBackground = C("#20FFFFFF"), KeyBadgeBorder = C("#15FFFFFF"), KeyBadgeText = C("#E0FFFFFF"),
        SearchBackground = C("#12FFFFFF"), SearchText = C("#E0FFFFFF"), SearchPlaceholder = C("#40FFFFFF"), SearchBorder = C("#10FFFFFF"),
        ShortcutRowBackground = C("#08FFFFFF"), ShortcutRowHover = C("#15FFFFFF"), ShortcutRowBorder = C("#08FFFFFF"),
        CategoryDivider = C("#10FFFFFF"),
        FooterButtonBackground = C("#10FFFFFF"), FooterButtonHover = C("#20FFFFFF"), FooterButtonText = C("#90FFFFFF"),
        AccentColor = C("#FF0A84FF"), AccentColorSubtle = C("#200A84FF"),
        ScrollbarThumb = C("#25FFFFFF"), ScrollbarTrack = C("#00000000"),
    };

    public static ThemePalette ClassicLight => new()
    {
        Name = "ClassicLight",
        OverlayBackground = C("#C8F5F5F7"), OverlayBorder = C("#20000000"),
        HeaderBackground = C("#08000000"),
        PrimaryText = C("#F0000000"), SecondaryText = C("#70000000"), TertiaryText = C("#40000000"),
        KeyBadgeBackground = C("#18000000"), KeyBadgeBorder = C("#10000000"), KeyBadgeText = C("#D0000000"),
        SearchBackground = C("#0C000000"), SearchText = C("#D0000000"), SearchPlaceholder = C("#40000000"), SearchBorder = C("#0A000000"),
        ShortcutRowBackground = C("#06000000"), ShortcutRowHover = C("#10000000"), ShortcutRowBorder = C("#06000000"),
        CategoryDivider = C("#10000000"),
        FooterButtonBackground = C("#08000000"), FooterButtonHover = C("#15000000"), FooterButtonText = C("#80000000"),
        AccentColor = C("#FF007AFF"), AccentColorSubtle = C("#20007AFF"),
        ScrollbarThumb = C("#20000000"), ScrollbarTrack = C("#00000000"),
    };

    // ── Midnight Blue ──
    public static ThemePalette MidnightBlueDark => new()
    {
        Name = "MidnightBlueDark",
        OverlayBackground = C("#C00A1628"), OverlayBorder = C("#18608BFF"),
        HeaderBackground = C("#0A4080D0"),
        PrimaryText = C("#F0D8E4FF"), SecondaryText = C("#80A0BBEE"), TertiaryText = C("#506888BB"),
        KeyBadgeBackground = C("#203060A0"), KeyBadgeBorder = C("#18406EBB"), KeyBadgeText = C("#E0C8DAFF"),
        SearchBackground = C("#12304880"), SearchText = C("#E0D0E0FF"), SearchPlaceholder = C("#405878AA"), SearchBorder = C("#10406EBB"),
        ShortcutRowBackground = C("#08203860"), ShortcutRowHover = C("#15305888"), ShortcutRowBorder = C("#08304878"),
        CategoryDivider = C("#10406090"),
        FooterButtonBackground = C("#10304880"), FooterButtonHover = C("#20406EBB"), FooterButtonText = C("#90A0C0EE"),
        AccentColor = C("#FF3B82F6"), AccentColorSubtle = C("#203B82F6"),
        ScrollbarThumb = C("#25406EBB"), ScrollbarTrack = C("#00000000"),
    };

    public static ThemePalette MidnightBlueLight => new()
    {
        Name = "MidnightBlueLight",
        OverlayBackground = C("#C8DEEAF8"), OverlayBorder = C("#20406EBB"),
        HeaderBackground = C("#0A2050A0"),
        PrimaryText = C("#F00A1830"), SecondaryText = C("#70183060"), TertiaryText = C("#40284878"),
        KeyBadgeBackground = C("#183060A0"), KeyBadgeBorder = C("#12406EBB"), KeyBadgeText = C("#D00A1830"),
        SearchBackground = C("#0C2050A0"), SearchText = C("#D00A1830"), SearchPlaceholder = C("#405070A0"), SearchBorder = C("#0A406EBB"),
        ShortcutRowBackground = C("#06203860"), ShortcutRowHover = C("#10305888"), ShortcutRowBorder = C("#06304878"),
        CategoryDivider = C("#10406090"),
        FooterButtonBackground = C("#082050A0"), FooterButtonHover = C("#15406EBB"), FooterButtonText = C("#80183060"),
        AccentColor = C("#FF2563EB"), AccentColorSubtle = C("#202563EB"),
        ScrollbarThumb = C("#20406EBB"), ScrollbarTrack = C("#00000000"),
    };

    // ── Rose Gold ──
    public static ThemePalette RoseGoldDark => new()
    {
        Name = "RoseGoldDark",
        OverlayBackground = C("#C02A1A1A"), OverlayBorder = C("#18FF9EAA"),
        HeaderBackground = C("#0AE08890"),
        PrimaryText = C("#F0FFE4E8"), SecondaryText = C("#80E0A8B0"), TertiaryText = C("#50B07880"),
        KeyBadgeBackground = C("#20A05060"), KeyBadgeBorder = C("#18C06878"), KeyBadgeText = C("#E0FFD8E0"),
        SearchBackground = C("#12804050"), SearchText = C("#E0FFE0E8"), SearchPlaceholder = C("#40A07080"), SearchBorder = C("#10C06878"),
        ShortcutRowBackground = C("#08603038"), ShortcutRowHover = C("#15884858"), ShortcutRowBorder = C("#08704048"),
        CategoryDivider = C("#10905060"),
        FooterButtonBackground = C("#10804050"), FooterButtonHover = C("#20C06878"), FooterButtonText = C("#90E0A0B0"),
        AccentColor = C("#FFF472B6"), AccentColorSubtle = C("#20F472B6"),
        ScrollbarThumb = C("#25C06878"), ScrollbarTrack = C("#00000000"),
    };

    public static ThemePalette RoseGoldLight => new()
    {
        Name = "RoseGoldLight",
        OverlayBackground = C("#C8F8E4EA"), OverlayBorder = C("#20C06878"),
        HeaderBackground = C("#0AA04858"),
        PrimaryText = C("#F03A1820"), SecondaryText = C("#70602838"), TertiaryText = C("#40804050"),
        KeyBadgeBackground = C("#18A04858"), KeyBadgeBorder = C("#12C06878"), KeyBadgeText = C("#D03A1820"),
        SearchBackground = C("#0CA04858"), SearchText = C("#D03A1820"), SearchPlaceholder = C("#40885060"), SearchBorder = C("#0AC06878"),
        ShortcutRowBackground = C("#06803848"), ShortcutRowHover = C("#10A04858"), ShortcutRowBorder = C("#06904050"),
        CategoryDivider = C("#10905060"),
        FooterButtonBackground = C("#08A04858"), FooterButtonHover = C("#15C06878"), FooterButtonText = C("#80602838"),
        AccentColor = C("#FFEC4899"), AccentColorSubtle = C("#20EC4899"),
        ScrollbarThumb = C("#20C06878"), ScrollbarTrack = C("#00000000"),
    };

    // ── Ocean Teal ──
    public static ThemePalette OceanTealDark => new()
    {
        Name = "OceanTealDark",
        OverlayBackground = C("#C00A2020"), OverlayBorder = C("#1850C8B8"),
        HeaderBackground = C("#0A30B0A0"),
        PrimaryText = C("#F0D8FFF8"), SecondaryText = C("#8090D8C8"), TertiaryText = C("#5060A098"),
        KeyBadgeBackground = C("#20208870"), KeyBadgeBorder = C("#1830A890"), KeyBadgeText = C("#E0D0FFF0"),
        SearchBackground = C("#12207060"), SearchText = C("#E0D8FFF0"), SearchPlaceholder = C("#405898A0"), SearchBorder = C("#1030A890"),
        ShortcutRowBackground = C("#08185040"), ShortcutRowHover = C("#15287860"), ShortcutRowBorder = C("#08206050"),
        CategoryDivider = C("#10308870"),
        FooterButtonBackground = C("#10207060"), FooterButtonHover = C("#2030A890"), FooterButtonText = C("#9090D0C0"),
        AccentColor = C("#FF2DD4BF"), AccentColorSubtle = C("#202DD4BF"),
        ScrollbarThumb = C("#2530A890"), ScrollbarTrack = C("#00000000"),
    };

    public static ThemePalette OceanTealLight => new()
    {
        Name = "OceanTealLight",
        OverlayBackground = C("#C8E0F5F0"), OverlayBorder = C("#2030A890"),
        HeaderBackground = C("#0A208870"),
        PrimaryText = C("#F00A2820"), SecondaryText = C("#70184838"), TertiaryText = C("#40286858"),
        KeyBadgeBackground = C("#18208870"), KeyBadgeBorder = C("#1230A890"), KeyBadgeText = C("#D00A2820"),
        SearchBackground = C("#0C208870"), SearchText = C("#D00A2820"), SearchPlaceholder = C("#40408878"), SearchBorder = C("#0A30A890"),
        ShortcutRowBackground = C("#06185040"), ShortcutRowHover = C("#10287860"), ShortcutRowBorder = C("#06206050"),
        CategoryDivider = C("#10308870"),
        FooterButtonBackground = C("#08208870"), FooterButtonHover = C("#1530A890"), FooterButtonText = C("#80184838"),
        AccentColor = C("#FF0D9488"), AccentColorSubtle = C("#200D9488"),
        ScrollbarThumb = C("#2030A890"), ScrollbarTrack = C("#00000000"),
    };

    // ── Forest Green ──
    public static ThemePalette ForestGreenDark => new()
    {
        Name = "ForestGreenDark",
        OverlayBackground = C("#C0121A0A"), OverlayBorder = C("#1868A060"),
        HeaderBackground = C("#0A50A048"),
        PrimaryText = C("#F0E8FFE8"), SecondaryText = C("#80A0D098"), TertiaryText = C("#5070A068"),
        KeyBadgeBackground = C("#20408838"), KeyBadgeBorder = C("#1858A850"), KeyBadgeText = C("#E0E0FFD8"),
        SearchBackground = C("#12387030"), SearchText = C("#E0E0FFE0"), SearchPlaceholder = C("#40689860"), SearchBorder = C("#1058A850"),
        ShortcutRowBackground = C("#08284828"), ShortcutRowHover = C("#15387038"), ShortcutRowBorder = C("#08305830"),
        CategoryDivider = C("#10488848"),
        FooterButtonBackground = C("#10387030"), FooterButtonHover = C("#2058A850"), FooterButtonText = C("#90A0D098"),
        AccentColor = C("#FF34D399"), AccentColorSubtle = C("#2034D399"),
        ScrollbarThumb = C("#2558A850"), ScrollbarTrack = C("#00000000"),
    };

    public static ThemePalette ForestGreenLight => new()
    {
        Name = "ForestGreenLight",
        OverlayBackground = C("#C8E8F5E8"), OverlayBorder = C("#2058A850"),
        HeaderBackground = C("#0A408838"),
        PrimaryText = C("#F01A2A10"), SecondaryText = C("#70284820"), TertiaryText = C("#40406838"),
        KeyBadgeBackground = C("#18408838"), KeyBadgeBorder = C("#1258A850"), KeyBadgeText = C("#D01A2A10"),
        SearchBackground = C("#0C408838"), SearchText = C("#D01A2A10"), SearchPlaceholder = C("#40588850"), SearchBorder = C("#0A58A850"),
        ShortcutRowBackground = C("#06284828"), ShortcutRowHover = C("#10387038"), ShortcutRowBorder = C("#06305830"),
        CategoryDivider = C("#10488848"),
        FooterButtonBackground = C("#08408838"), FooterButtonHover = C("#1558A850"), FooterButtonText = C("#80284820"),
        AccentColor = C("#FF059669"), AccentColorSubtle = C("#20059669"),
        ScrollbarThumb = C("#2058A850"), ScrollbarTrack = C("#00000000"),
    };

    // ── Sunset Amber ──
    public static ThemePalette SunsetAmberDark => new()
    {
        Name = "SunsetAmberDark",
        OverlayBackground = C("#C02A1E0E"), OverlayBorder = C("#18E0A050"),
        HeaderBackground = C("#0AD09040"),
        PrimaryText = C("#F0FFF0D8"), SecondaryText = C("#80D8B880"), TertiaryText = C("#50A88850"),
        KeyBadgeBackground = C("#20A07828"), KeyBadgeBorder = C("#18C09038"), KeyBadgeText = C("#E0FFF0D0"),
        SearchBackground = C("#12806020"), SearchText = C("#E0FFF0D8"), SearchPlaceholder = C("#40A08848"), SearchBorder = C("#10C09038"),
        ShortcutRowBackground = C("#08604818"), ShortcutRowHover = C("#15886828"), ShortcutRowBorder = C("#08705020"),
        CategoryDivider = C("#10907040"),
        FooterButtonBackground = C("#10806020"), FooterButtonHover = C("#20C09038"), FooterButtonText = C("#90D0B070"),
        AccentColor = C("#FFFBBF24"), AccentColorSubtle = C("#20FBBF24"),
        ScrollbarThumb = C("#25C09038"), ScrollbarTrack = C("#00000000"),
    };

    public static ThemePalette SunsetAmberLight => new()
    {
        Name = "SunsetAmberLight",
        OverlayBackground = C("#C8F8F0E0"), OverlayBorder = C("#20C09038"),
        HeaderBackground = C("#0AA07828"),
        PrimaryText = C("#F02A1E0A"), SecondaryText = C("#70503818"), TertiaryText = C("#40785828"),
        KeyBadgeBackground = C("#18A07828"), KeyBadgeBorder = C("#12C09038"), KeyBadgeText = C("#D02A1E0A"),
        SearchBackground = C("#0CA07828"), SearchText = C("#D02A1E0A"), SearchPlaceholder = C("#40887040"), SearchBorder = C("#0AC09038"),
        ShortcutRowBackground = C("#06604818"), ShortcutRowHover = C("#10886828"), ShortcutRowBorder = C("#06705020"),
        CategoryDivider = C("#10907040"),
        FooterButtonBackground = C("#08A07828"), FooterButtonHover = C("#15C09038"), FooterButtonText = C("#80503818"),
        AccentColor = C("#FFD97706"), AccentColorSubtle = C("#20D97706"),
        ScrollbarThumb = C("#20C09038"), ScrollbarTrack = C("#00000000"),
    };

    /// <summary>
    /// All theme families as (familyKey, dark, light) tuples.
    /// </summary>
    public static IReadOnlyList<(string Key, string DisplayName, ThemePalette Dark, ThemePalette Light)> Families { get; } = new[]
    {
        ("Classic",       "Classic",        ClassicDark,       ClassicLight),
        ("MidnightBlue",  "Midnight Blue",  MidnightBlueDark,  MidnightBlueLight),
        ("RoseGold",      "Rose Gold",      RoseGoldDark,      RoseGoldLight),
        ("OceanTeal",     "Ocean Teal",     OceanTealDark,     OceanTealLight),
        ("ForestGreen",   "Forest Green",   ForestGreenDark,   ForestGreenLight),
        ("SunsetAmber",   "Sunset Amber",   SunsetAmberDark,   SunsetAmberLight),
    };
}
