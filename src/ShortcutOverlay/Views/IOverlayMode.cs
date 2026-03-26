namespace ShortcutOverlay.Views;

public interface IOverlayMode
{
    void ShowOverlay();
    void HideOverlay();
    void ToggleVisibility();
    bool IsOverlayVisible { get; }
}
