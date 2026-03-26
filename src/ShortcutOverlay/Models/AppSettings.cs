namespace ShortcutOverlay.Models;

public record AppSettings
{
    public string DisplayMode { get; init; } = "floating";
    public string GlobalHotkey { get; init; } = "Ctrl+Shift+S";
    public double Opacity { get; init; } = 0.85;
    public string Theme { get; init; } = "auto";
    public string DockSide { get; init; } = "right";
    public bool StartWithWindows { get; init; } = false;
    public bool AutoSwitchProfiles { get; init; } = true;
    public bool ShowOnAllDesktops { get; init; } = true;
    public string FontSize { get; init; } = "medium";
    public PositionDto FloatingPosition { get; init; } = new(1500, 200);
    public int SidePanelWidth { get; init; } = 280;
    public bool AlwaysOnTop { get; init; } = true;
}

public record PositionDto(double X, double Y);
