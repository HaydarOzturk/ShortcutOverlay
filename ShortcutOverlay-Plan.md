# Shortcut Saver Overlay — Detailed Project Plan

**Platform:** Windows 10/11
**Tech Stack:** C# / WPF (.NET 8+)
**Project Type:** Desktop overlay application
**Date:** March 26, 2026

---

## 1. Project Overview

A lightweight, always-on-top overlay application that displays keyboard shortcuts for the currently active application. The overlay detects which window is in the foreground (e.g., VS Code, Chrome, Command Prompt, Desktop) and automatically switches to the relevant shortcut cheat-sheet — all without interrupting your workflow.

### Core Value Proposition

- **Zero friction** — runs silently in the system tray; toggle visibility with a global hotkey
- **Context-aware** — automatically detects the active application and shows its shortcuts
- **Three display modes** — side panel, floating widget, or system tray popup
- **Fully customizable** — users can add, edit, and organize their own shortcuts
- **Lightweight** — minimal CPU/RAM usage; no Electron bloat

---

## 2. Feature Breakdown

### 2.1 Active Window Detection

| Aspect | Detail |
|---|---|
| **API** | Win32 `SetWinEventHook` with `EVENT_SYSTEM_FOREGROUND` (event-driven, no polling) |
| **Fallback** | `GetForegroundWindow()` + `DispatcherTimer` polling at 500ms intervals |
| **Window identification** | Extract process name via `GetWindowThreadProcessId` → `Process.GetProcessById` → `Process.ProcessName` |
| **Special cases** | Desktop (`explorer.exe` with class `Progman` or `WorkerW`), UWP apps (use `ApplicationFrameHost` → child window inspection) |

**How it works:**
1. On app startup, register a `WinEventHook` for `EVENT_SYSTEM_FOREGROUND`
2. When the active window changes, the callback fires
3. Extract the process name (e.g., `chrome`, `cmd`, `Code`, `explorer`)
4. Look up the matching shortcut profile from the data store
5. Update the overlay UI with the new shortcut set
6. If no profile exists → show a "No shortcuts found — Add some?" prompt

### 2.2 Display Modes (User-Selectable)

#### Mode A: Side Panel
- A slim, semi-transparent panel docked to the left or right screen edge
- Default width: ~280px, full screen height
- Auto-hides when the mouse leaves; slides in on hover or hotkey
- Supports drag-to-resize width
- Opacity slider (20%–100%)

#### Mode B: Floating Widget
- A small, draggable overlay window (default ~320×450px)
- Toggle open/close with a global hotkey (default: `Ctrl+Shift+S`)
- Remembers last position on screen
- Rounded corners, shadow, modern card-style design
- Minimize to a tiny "pill" indicator showing the current app icon

#### Mode C: System Tray Popup
- Lives as an icon in the Windows system tray
- Left-click or hotkey opens a popup card near the tray area
- Popup shows shortcuts in a compact, scrollable card
- Right-click tray icon → context menu (Settings, Quit, Switch Mode)

### 2.3 Shortcut Data Management

- **Storage format:** JSON files, one per application profile
- **Location:** `%AppData%/ShortcutOverlay/profiles/`
- **Built-in profiles:** Ship with 10–15 pre-built profiles (see Section 6)
- **Custom profiles:** Users can create/edit/delete via a settings UI
- **Categories:** Shortcuts within a profile are grouped by category (e.g., "Navigation", "Editing", "File")
- **Search/filter:** Type-to-filter within the overlay to find a shortcut quickly

### 2.4 Settings & Preferences

| Setting | Options | Default |
|---|---|---|
| Display mode | Side Panel / Floating / Tray Popup | Floating Widget |
| Global hotkey | Customizable key combo | `Ctrl+Shift+S` |
| Opacity | Slider 20%–100% | 85% |
| Theme | Light / Dark / Auto (match Windows) | Auto |
| Dock side (panel mode) | Left / Right | Right |
| Start with Windows | Toggle | Off |
| Auto-switch profiles | Toggle | On |
| Show on all virtual desktops | Toggle | On |
| Font size | Small / Medium / Large | Medium |

---

## 3. Architecture

### 3.1 Solution Structure

```
ShortcutOverlay/
├── ShortcutOverlay.sln
├── src/
│   ├── ShortcutOverlay/                    # Main WPF application
│   │   ├── App.xaml / App.xaml.cs          # App entry, single-instance guard
│   │   ├── Models/
│   │   │   ├── ShortcutProfile.cs          # Profile data model
│   │   │   ├── ShortcutEntry.cs            # Individual shortcut
│   │   │   ├── ShortcutCategory.cs         # Category grouping
│   │   │   └── AppSettings.cs              # User preferences model
│   │   ├── Services/
│   │   │   ├── WindowDetectionService.cs   # Win32 hook for active window
│   │   │   ├── ProfileManager.cs           # Load/save/switch profiles
│   │   │   ├── HotkeyService.cs            # Global hotkey registration
│   │   │   ├── SettingsService.cs           # Settings persistence
│   │   │   └── TrayIconService.cs          # System tray management
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs            # Primary VM with profile state
│   │   │   ├── SettingsViewModel.cs         # Settings page VM
│   │   │   └── ProfileEditorViewModel.cs   # Add/edit shortcuts VM
│   │   ├── Views/
│   │   │   ├── SidePanelWindow.xaml         # Docked side panel
│   │   │   ├── FloatingWidgetWindow.xaml    # Draggable overlay
│   │   │   ├── TrayPopupWindow.xaml         # Tray popup card
│   │   │   ├── SettingsWindow.xaml           # Settings UI
│   │   │   └── ProfileEditorWindow.xaml     # Shortcut editor
│   │   ├── Controls/
│   │   │   ├── ShortcutListControl.xaml     # Reusable shortcut list
│   │   │   ├── SearchBox.xaml               # Filter/search control
│   │   │   └── KeyComboDisplay.xaml         # Styled key combo badge
│   │   ├── Converters/
│   │   │   └── (value converters for UI)
│   │   ├── NativeInterop/
│   │   │   ├── Win32Api.cs                 # P/Invoke declarations
│   │   │   └── WindowHookManager.cs        # SetWinEventHook wrapper
│   │   ├── Resources/
│   │   │   ├── Styles/                     # XAML styles & themes
│   │   │   ├── Icons/                      # App & tray icons
│   │   │   └── DefaultProfiles/            # Built-in JSON profiles
│   │   └── Helpers/
│   │       ├── SingleInstanceGuard.cs      # Prevent duplicate launches
│   │       └── AnimationHelper.cs          # Slide-in/fade animations
│   └── ShortcutOverlay.Tests/             # Unit tests
│       ├── Services/
│       └── ViewModels/
├── profiles/                               # Sample shortcut JSON files
└── docs/
    └── (this plan + wireframes)
```

### 3.2 Key Design Patterns

- **MVVM** — Clean separation of Views, ViewModels, and Models using `CommunityToolkit.Mvvm`
- **Dependency Injection** — `Microsoft.Extensions.DependencyInjection` for service registration
- **Observer pattern** — `WindowDetectionService` raises events; `MainViewModel` subscribes
- **Strategy pattern** — Display modes implement a common `IOverlayMode` interface so switching is seamless

### 3.3 Component Interaction Flow

```
┌──────────────────┐     EVENT_SYSTEM_FOREGROUND      ┌──────────────────┐
│  Windows OS      │ ──────────────────────────────►  │ WindowDetection  │
│  (Foreground     │                                   │ Service          │
│   window change) │                                   └────────┬─────────┘
└──────────────────┘                                            │
                                                                │ ActiveAppChanged event
                                                                ▼
┌──────────────────┐     Loads matching profile        ┌──────────────────┐
│  ProfileManager  │ ◄──────────────────────────────── │ MainViewModel    │
│  (JSON store)    │ ────────────────────────────────► │ (orchestrator)   │
└──────────────────┘     Returns ShortcutProfile        └────────┬─────────┘
                                                                │
                                                                │ Data binding
                                                                ▼
                                                       ┌──────────────────┐
                                                       │  Active Overlay  │
                                                       │  Window (any of  │
                                                       │  3 display modes)│
                                                       └──────────────────┘
```

---

## 4. Win32 Interop — Key Code

### 4.1 P/Invoke Declarations Needed

```csharp
// Core APIs to import from user32.dll:
SetWinEventHook          // Register for foreground window change events
UnhookWinEvent           // Clean up on exit
GetForegroundWindow      // Get current active window handle
GetWindowThreadProcessId // Get PID from window handle
GetClassName             // Identify special windows (Desktop, UWP frames)
RegisterHotKey           // Global hotkey registration
UnregisterHotKey         // Clean up hotkeys
```

### 4.2 Window Detection Logic (Pseudocode)

```
OnForegroundChanged(hwnd):
    processId = GetWindowThreadProcessId(hwnd)
    process = Process.GetProcessById(processId)
    processName = process.ProcessName.ToLower()

    // Special case: Desktop
    if processName == "explorer":
        className = GetClassName(hwnd)
        if className in ["Progman", "WorkerW"]:
            return "desktop"
        else:
            return "file-explorer"

    // Special case: UWP apps (ApplicationFrameHost)
    if processName == "applicationframehost":
        childProcess = GetUwpChildProcess(hwnd)
        return childProcess.ProcessName

    // Special case: Windows Terminal (multiple shells)
    if processName == "windowsterminal":
        return DetectShellType(hwnd)  // cmd, powershell, wsl, etc.

    return processName
```

### 4.3 Overlay Window Properties

```xml
<!-- Key XAML attributes for the overlay windows -->
<Window
    Topmost="True"
    ShowInTaskbar="False"
    AllowsTransparency="True"
    WindowStyle="None"
    Background="Transparent"
    ResizeMode="NoResize" />
```

---

## 5. Data Schema

### 5.1 Shortcut Profile JSON Format

```json
{
  "profileId": "vscode",
  "displayName": "Visual Studio Code",
  "processNames": ["Code"],
  "icon": "vscode.png",
  "categories": [
    {
      "name": "General",
      "shortcuts": [
        {
          "keys": "Ctrl+Shift+P",
          "description": "Command Palette",
          "tags": ["command", "search"]
        },
        {
          "keys": "Ctrl+P",
          "description": "Quick Open file",
          "tags": ["file", "search"]
        }
      ]
    },
    {
      "name": "Editing",
      "shortcuts": [
        {
          "keys": "Ctrl+D",
          "description": "Select next occurrence",
          "tags": ["select", "multi-cursor"]
        }
      ]
    }
  ]
}
```

### 5.2 Settings JSON Format

```json
{
  "displayMode": "floating",
  "globalHotkey": "Ctrl+Shift+S",
  "opacity": 0.85,
  "theme": "auto",
  "dockSide": "right",
  "startWithWindows": false,
  "autoSwitchProfiles": true,
  "showOnAllDesktops": true,
  "fontSize": "medium",
  "floatingPosition": { "x": 1500, "y": 200 },
  "sidePanelWidth": 280
}
```

---

## 6. Built-in Shortcut Profiles to Ship With

| # | Application | Process Name(s) | Priority |
|---|---|---|---|
| 1 | Desktop (Windows) | `explorer` (Progman) | Must-have |
| 2 | Command Prompt | `cmd` | Must-have |
| 3 | PowerShell | `powershell`, `pwsh` | Must-have |
| 4 | Windows Terminal | `WindowsTerminal` | Must-have |
| 5 | File Explorer | `explorer` (CabinetWClass) | Must-have |
| 6 | Google Chrome | `chrome` | Must-have |
| 7 | Visual Studio Code | `Code` | Must-have |
| 8 | Microsoft Word | `WINWORD` | Should-have |
| 9 | Microsoft Excel | `EXCEL` | Should-have |
| 10 | Notepad / Notepad++ | `notepad`, `notepad++` | Should-have |
| 11 | Visual Studio | `devenv` | Nice-to-have |
| 12 | Blender | `blender` | Nice-to-have |
| 13 | Unreal Engine 5 | `UnrealEditor` | Nice-to-have |
| 14 | Adobe Photoshop | `Photoshop` | Nice-to-have |
| 15 | Substance Painter | `Adobe Substance 3D Painter`, `painter` | Nice-to-have |

---

## 7. UI/UX Design Specifications

### 7.1 Visual Style

- **Theme:** Follows Windows system theme (light/dark) by default via `SystemParameters`
- **Corner radius:** 12px for floating windows, 0 for side panel
- **Backdrop:** Acrylic/Mica blur effect (Win11) or solid semi-transparent (Win10)
- **Key combo badges:** Rounded pill shapes with a subtle border, monospace font (e.g., `Cascadia Code`)
- **Accent color:** Matches Windows accent color via `SystemParameters.WindowGlassBrush`

### 7.2 Floating Widget Wireframe

```
╭──────────────────────────────╮
│ 🔍 Search shortcuts...       │
├──────────────────────────────┤
│ ▸ VS Code                ▾  │  ← App name + dropdown to manually switch
├──────────────────────────────┤
│                              │
│  GENERAL                     │
│  ┌────────┐                  │
│  │Ctrl+S  │  Save file       │
│  └────────┘                  │
│  ┌──────────────┐            │
│  │Ctrl+Shift+P  │  Commands  │
│  └──────────────┘            │
│  ┌────────┐                  │
│  │Ctrl+P  │  Quick open      │
│  └────────┘                  │
│                              │
│  EDITING                     │
│  ┌────────┐                  │
│  │Ctrl+D  │  Select next     │
│  └────────┘                  │
│  ┌──────────────┐            │
│  │Ctrl+Shift+K  │  Del line  │
│  └──────────────┘            │
│                              │
│  ┌────────┐  ┌──────────┐    │
│  │ Edit ✏ │  │ Settings ⚙│   │  ← Bottom action bar
│  └────────┘  └──────────┘    │
╰──────────────────────────────╯
```

### 7.3 Animations

- **Floating widget:** Fade-in (200ms ease-out) on hotkey toggle
- **Side panel:** Slide from edge (250ms cubic-bezier)
- **Tray popup:** Scale-up from tray icon position (200ms)
- **Profile switch:** Crossfade content (150ms) when active app changes

---

## 8. Implementation Phases

### Phase 1 — Core Foundation (Week 1–2)

- [ ] Set up .NET 8 WPF project with MVVM structure
- [ ] Implement `WindowDetectionService` with `SetWinEventHook`
- [ ] Create `ShortcutProfile` / `ShortcutEntry` data models
- [ ] Build `ProfileManager` to load/save JSON profiles
- [ ] Create 3 starter profiles (Desktop, CMD, Chrome)
- [ ] Build basic floating widget window with shortcut list display
- [ ] Wire up auto-switching: window change → profile switch → UI update

**Milestone:** A floating window that auto-shows Chrome shortcuts when Chrome is active, CMD shortcuts in Command Prompt, etc.

### Phase 2 — All Display Modes + Hotkey (Week 3)

- [ ] Implement `HotkeyService` for global hotkey registration
- [ ] Build side panel (docked, auto-hide, resizable)
- [ ] Build system tray popup + `TrayIconService` using `NotifyIcon`
- [ ] Add mode switcher in settings
- [ ] Implement `IOverlayMode` interface so all three share `ShortcutListControl`

**Milestone:** All three display modes functional with hotkey toggle.

### Phase 3 — Settings & Profile Editor (Week 4)

- [ ] Build settings window (all options from Section 2.4)
- [ ] Build profile editor: add/edit/delete shortcuts and categories
- [ ] Implement search/filter within the overlay
- [ ] Add manual profile dropdown (override auto-detection)
- [ ] Implement "Start with Windows" via Registry or Startup folder
- [ ] Persist window positions, sizes, and preferences

**Milestone:** Users can fully customize their experience and add their own shortcuts.

### Phase 4 — Polish & Built-in Profiles (Week 5)

- [ ] Create all 15 built-in shortcut profiles with accurate data
- [ ] Implement light/dark/auto theme switching
- [ ] Add Mica/Acrylic backdrop for Windows 11
- [ ] Add animations (slide, fade, crossfade)
- [ ] Handle edge cases: UWP apps, Windows Terminal shell detection, multi-monitor
- [ ] Single-instance guard (prevent duplicate launches)

**Milestone:** Polished, production-quality app with rich built-in content.

### Phase 5 — Advanced Features (Week 6+, optional)

- [ ] Import/export profiles (share with others)
- [ ] Profile marketplace / community sharing
- [ ] "Learn mode" — highlight a shortcut you haven't used today
- [ ] Usage tracking — show which shortcuts you use most
- [ ] Auto-detect installed apps and suggest profiles to enable
- [ ] Clickable shortcuts (simulate the key press when clicked)
- [ ] Print-friendly cheat sheet export (PDF)

---

## 9. NuGet Dependencies

| Package | Purpose |
|---|---|
| `CommunityToolkit.Mvvm` | MVVM base classes, `ObservableObject`, `RelayCommand` |
| `Microsoft.Extensions.DependencyInjection` | DI container |
| `System.Text.Json` | JSON serialization for profiles/settings |
| `Hardcodet.NotifyIcon.Wpf` | System tray icon support |
| `Microsoft.Xaml.Behaviors.Wpf` | Blend behaviors (drag, auto-hide) |

No Electron. No Node.js. Pure .NET — fast startup, low memory (~25–40 MB).

---

## 10. Risks & Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| UWP apps report as `ApplicationFrameHost` | Wrong profile shown | Inspect child window process to get the real app |
| Windows Terminal runs multiple shells | Can't tell if user is in CMD vs PowerShell | Use Terminal's title bar text or console API to detect shell |
| Full-screen games override Topmost | Overlay hidden during gaming | Use `SetWindowPos` with `HWND_TOPMOST` flag; optionally auto-hide in full-screen apps |
| Global hotkey conflicts | Hotkey already used by another app | Allow user to customize; show conflict warning |
| High-DPI / multi-monitor | UI scaling issues | Use per-monitor DPI awareness in app manifest |

---

## 11. Testing Strategy

- **Unit tests:** `ProfileManager` JSON round-trip, `WindowDetectionService` process name mapping, settings persistence
- **Integration tests:** Hook lifecycle (register → callback → unhook), profile auto-switching
- **Manual tests:** All 3 display modes on Windows 10 and 11, multi-monitor, high-DPI scaling, virtual desktops, full-screen apps
- **Edge cases:** No matching profile, corrupted JSON, rapid window switching, UWP apps, admin-elevated windows

---

## 12. Claude Skills for This Project

Two custom Claude skills have been created to ensure consistent, high-quality development throughout this project. These are located in `skills/` within the project folder.

### 12.1 `wpf-overlay` Skill

**Location:** `skills/wpf-overlay/`

Guides Claude on the full architecture, coding conventions, and gotchas specific to this project. Includes ready-to-use code templates in `references/`:

| Reference File | Contains |
|---|---|
| `references/win32-interop.md` | All P/Invoke declarations, SetWinEventHook, window detection, RegisterHotKey, UWP detection, Topmost fix, Alt+Tab hiding |
| `references/mvvm-patterns.md` | DI container setup, MainViewModel, ProfileManager, IOverlayMode interface, data models, DTO pattern |
| `references/overlay-windows.md` | XAML for all 3 display modes, ShortcutListControl, KeyComboDisplay, Mica/Acrylic helper, animations, DPI manifest, theme support |

The skill also documents the 10 critical gotchas (delegate GC, Topmost bug, silent hotkey failures, etc.) so Claude avoids known pitfalls.

### 12.2 `git-workflow` Skill

**Location:** `skills/git-workflow/`

Ensures every code change is tracked with proper version control. Key behaviors:

- **Proactive git reminders** — Claude always provides pull/push commands after code changes
- **Branching strategy** — `phase-N/description` branches aligned with project phases
- **Conventional commits** — `feat(detection):`, `fix(overlay):`, etc.
- **Release tagging** — `v0.1.0` through `v1.0.0` milestone scheme
- **PR workflow** — Templates for phase completion pull requests

Claude will never let code changes go uncommitted or unpushed.

---

## 13. Summary

This app solves a simple but persistent problem: you're using an application and you *know* there's a shortcut for what you're doing, but you can't remember it. Instead of alt-tabbing to Google, you glance at the overlay, see the shortcut, and keep working. The auto-switching means you never have to manually look anything up — the right cheat sheet is always right there.

The C#/WPF stack keeps it native, fast, and lightweight. The MVVM architecture keeps it maintainable. And the three display modes mean every user can find the style that fits their workflow.
