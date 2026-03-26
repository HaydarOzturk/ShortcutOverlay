# Phase 3: Minimalist UI Redesign — Implementation Plan

## Design Goals
- Super minimalistic: no long names, no text buttons, no menu bars
- Icon-based controls that communicate by design
- Right-click context menu for settings/configuration
- Taller overlay (600px default) to reduce scrolling
- Compact typography: smaller descriptions, larger key badges
- Category drag-and-drop reordering (persisted per profile)

---

## Change Summary (7 Steps)

### Step 1: Remove Footer Buttons, Add Icon Tray
**Files**: `FloatingWidgetWindow.xaml`, `BaseStyles.xaml`

Remove the "Edit" and "Settings" text buttons from the footer. Replace with a slim icon tray at the top-right of the header (inline with app name).

**Icons** (Unicode/Segoe MDL2 Assets — no external assets needed):
- `⋮` (U+22EE) or `⚙` — opens context menu on left-click (replaces hamburger)
- Pin/Unpin toggle: `📌` pinned / `📍` unpinned (toggle always-on-top behavior)

**Header layout** (before → after):
```
BEFORE:  [  Google Chrome                    ☰  ]
AFTER:   [ 🔵 Chrome            📌  ⋮ ]
```

The `⋮` icon is the ONLY interactive element. Everything else goes through the right-click context menu.

**Implementation**:
- Remove Grid.Row="3" (footer) from FloatingWidgetWindow.xaml
- Change RowDefinitions from 4 rows to 3 rows (header, search, content)
- Update header Grid to: [AppIcon 16px] [AppName truncated] [Spacer] [Pin icon] [Menu icon]
- Both Pin and Menu icons are clickable TextBlocks with Cursor="Hand"
- Menu icon opens the context menu programmatically

---

### Step 2: Right-Click Context Menu
**Files**: `FloatingWidgetWindow.xaml`, `FloatingWidgetWindow.xaml.cs`, `BaseStyles.xaml`

Add a styled ContextMenu to the entire window (right-click anywhere) AND to the ⋮ icon (left-click).

**Menu items**:
```
🎨  Theme          → [submenu: Classic, Midnight Blue, Rose Gold, Ocean Teal, Forest Green, Sunset Amber, Adaptive]
◐  Opacity         → [submenu: 60%, 70%, 80%, 90%, 100%]
📐  Display Mode   → [submenu: Floating Widget ✓, Side Panel, Tray Popup]
✏️  Edit Shortcuts  (opens profile editor — Phase 3 future)
⚙  Settings        (opens settings window — Phase 3 future)
ℹ  About
─────────────────
✕  Quit
```

**Implementation**:
- Define ContextMenu in XAML as a Window resource
- Style it with glass appearance (match overlay theme)
- Bind Theme submenu to ThemePalette.Families
- Bind Opacity to slider values via SettingsService
- Wire commands to MainViewModel relay commands
- Assign same ContextMenu to Window.ContextMenu AND ⋮ icon click handler

---

### Step 3: Increase Overlay Height + Compact Layout
**Files**: `FloatingWidgetWindow.xaml`, `ShortcutListControl.xaml`, `KeyComboDisplay.xaml`

**Window changes**:
- Height: 500 → 600
- Width: 340 → 340 (unchanged)
- Header padding: 14,12 → 10,8 (more compact)
- Search padding: 12,8 → 8,6

**ShortcutListControl changes** (the big space saver):
- Category name: FontSize 12 → 11, reduce top margin
- Shortcut row padding: 10,8 → 8,5 (less vertical spacing)
- Remove per-row bottom border (use subtle category-level dividers only)
- Description FontSize: 13 → 12

**KeyComboDisplay changes**:
- Badge padding: 7,3 → 6,2 (slightly tighter)
- Badge FontSize: stays 12 (keys should be prominent)
- Badge margin: 3,0,0,0 → 2,0,0,0

**Net effect**: ~30% more shortcuts visible without scrolling.

---

### Step 4: Differentiated Typography
**Files**: `ShortcutListControl.xaml`, `KeyComboDisplay.xaml`

Current problem: key badges and description text look the same size, blurring the visual hierarchy.

**Fix**:
- Key badge text: 12px, SemiBold, full opacity → the HERO element
- Description text: 11.5px, Normal weight, 85% opacity → supporting role
- Category headers: 10px, ALL CAPS, letter-spacing 1px, tertiary color → quiet section labels

This creates a clear visual hierarchy: **Keys** > Description > Category name.

---

### Step 5: Category Drag & Drop Reordering
**Files**: `ShortcutListControl.xaml`, `ShortcutListControl.xaml.cs`, `ShortcutProfile.cs`, `ProfileManager.cs`

Allow users to drag category headers to reorder them. The order is saved per profile.

**Implementation approach** (low-risk, no third-party libs):
1. Each category header gets a drag handle (⠿ grip icon, subtle)
2. MouseDown on grip → start drag (DragDrop.DoDragDrop with DataObject)
3. DragOver on other categories → show insertion indicator (blue line)
4. Drop → reorder the Categories list in the profile
5. Save the new order to the profile JSON via ProfileManager

**Data model change**:
- Add `SortOrder: int` to ShortcutCategory (optional, default = index in list)
- ProfileManager.SaveCategoryOrder(profileId, orderedCategoryNames)
- On load, sort categories by SortOrder

**Visual indicators**:
- Drag handle: `⠿` (U+2807) or `≡` (U+2261), tertiary text color, appears on hover
- Drop target: 2px accent-colored line between categories
- Dragging: slight opacity reduction on source item

**Risk mitigation**:
- Use WPF's built-in DragDrop (no external library)
- Keep the drag visual simple (just opacity change, no ghost preview)
- Save order only on drop complete (not during drag)

---

### Step 6: Apply to SidePanel and TrayPopup
**Files**: `SidePanelWindow.xaml`, `TrayPopupWindow.xaml`

Mirror the same changes to the other two display modes:
- Same compact header with icon + short name + ⋮
- Same right-click context menu
- Same compact shortcut layout
- No footer buttons
- SidePanel: keep auto-hide behavior, apply compact styles
- TrayPopup: keep deactivation-hide, apply compact styles

---

### Step 7: Persist Settings
**Files**: `AppSettings.cs`, `SettingsService.cs`

Add new settings fields:
```csharp
// In AppSettings.cs
public double OverlayOpacity { get; init; } = 0.85;
public bool AlwaysOnTop { get; init; } = true;
// CategoryOrder is stored in each profile, not in AppSettings
```

Context menu changes to Theme, Opacity, DisplayMode, and AlwaysOnTop all persist through SettingsService.

---

## Execution Order

**Why this order matters**: Each step builds on the previous without breaking anything. Steps 1-4 are pure UI/XAML changes with zero logic risk. Step 5 adds the only new behavior (drag-drop). Steps 6-7 are mechanical propagation.

| Step | Risk | Time Est. | Dependencies |
|------|------|-----------|-------------|
| 1. Remove footer, add icon tray | Low | Small | None |
| 2. Context menu | Low | Medium | Step 1 (menu icon) |
| 3. Height + compact layout | Low | Small | None |
| 4. Typography hierarchy | Low | Small | Step 3 |
| 5. Drag & drop categories | Medium | Medium | Step 4 |
| 6. Apply to SidePanel/Tray | Low | Small | Steps 1-4 |
| 7. Persist settings | Low | Small | Step 2 |

---

## What We're NOT Changing
- Adaptive theme detection (just finished, working well)
- ThemeAnimator / ThemePalette system
- WindowDetectionService / ProfileManager core logic
- MVVM architecture
- DynamicResource binding pattern
- Win32 interop layer

## Files Touched (Complete List)
- `Views/FloatingWidgetWindow.xaml` — major layout change
- `Views/FloatingWidgetWindow.xaml.cs` — context menu handlers, pin toggle
- `Views/SidePanelWindow.xaml` — mirror layout changes
- `Views/TrayPopupWindow.xaml` — mirror layout changes
- `Controls/ShortcutListControl.xaml` — compact layout + drag zones
- `Controls/ShortcutListControl.xaml.cs` — drag & drop logic
- `Controls/KeyComboDisplay.xaml` — tighter badges
- `Resources/Styles/BaseStyles.xaml` — context menu style, remove footer button style
- `Models/AppSettings.cs` — new fields
- `Models/ShortcutCategory.cs` — SortOrder field
- `Services/SettingsService.cs` — persist new settings
- `Services/ProfileManager.cs` — save category order
- `ViewModels/MainViewModel.cs` — new commands for context menu actions
