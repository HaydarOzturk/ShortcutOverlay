namespace ShortcutOverlay.Models;

public record ShortcutProfile
{
    public string ProfileId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public List<string> ProcessNames { get; init; } = new();
    public List<string> WindowClasses { get; init; } = new();
    public string Icon { get; init; } = string.Empty;
    public List<ShortcutCategory> Categories { get; init; } = new();
}

public record ShortcutCategory
{
    public string Name { get; init; } = string.Empty;
    public int SortOrder { get; init; } = 0;
    public List<ShortcutEntry> Shortcuts { get; init; } = new();
}

public record ShortcutEntry
{
    public string Keys { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public List<string> Tags { get; init; } = new();

    /// <summary>
    /// Splits the Keys string (e.g. "Ctrl+Shift+S") into individual parts for key badge display.
    /// </summary>
    public IReadOnlyList<string> KeyParts => Keys.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
