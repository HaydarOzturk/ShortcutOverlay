---
name: wpf-overlay
description: >
  C# WPF overlay application development skill for the ShortcutOverlay project — a Windows desktop app that
  detects the active foreground window and displays relevant keyboard shortcuts in a non-intrusive overlay.
  Use this skill whenever working on: C# WPF overlay windows, Win32 P/Invoke for window detection
  (SetWinEventHook, GetForegroundWindow, RegisterHotKey), transparent always-on-top WPF windows,
  system tray integration, MVVM with CommunityToolkit.Mvvm, Mica/Acrylic backdrop effects,
  per-monitor DPI awareness, or JSON-based shortcut profile management. Also trigger when the user
  mentions "shortcut overlay", "shortcut saver", active window detection, or building a WPF desktop tool
  that sits on top of other windows.
---

# ShortcutOverlay — C# WPF Development Skill

This skill guides the development of ShortcutOverlay, a lightweight Windows overlay that shows keyboard
shortcuts for whatever application is currently in focus. It auto-switches profiles when the user changes
windows, supports three display modes (side panel, floating widget, system tray popup), and uses
event-driven Win32 hooks rather than polling.

## Architecture Overview

The app follows **MVVM** with `CommunityToolkit.Mvvm` and uses **dependency injection** via
`Microsoft.Extensions.DependencyInjection`. The core flow is:

1. `WindowDetectionService` hooks into `EVENT_SYSTEM_FOREGROUND` via `SetWinEventHook`
2. When the active window changes, it extracts the process name and raises `ActiveAppChanged`
3. `MainViewModel` subscribes to this event, asks `ProfileManager` for a matching profile
4. The overlay UI updates via data binding — whichever display mode is active shows the new shortcuts

Key services:
- `WindowDetectionService` — Win32 hook for detecting foreground window changes
- `ProfileManager` — loads/saves JSON shortcut profiles from `%AppData%/ShortcutOverlay/profiles/`
- `HotkeyService` — global hotkey registration via `RegisterHotKey`
- `TrayIconService` — system tray icon using `Hardcodet.NotifyIcon.Wpf`
- `SettingsService` — persists user preferences as JSON

All three display modes share a common `ShortcutListControl` and implement `IOverlayMode` so switching
between them is seamless.

## Project Structure

```
src/ShortcutOverlay/
├── Models/          ShortcutProfile, ShortcutEntry, ShortcutCategory, AppSettings
├── Services/        WindowDetectionService, ProfileManager, HotkeyService, SettingsService, TrayIconService
├── ViewModels/      MainViewModel, SettingsViewModel, ProfileEditorViewModel
├── Views/           SidePanelWindow, FloatingWidgetWindow, TrayPopupWindow, SettingsWindow
├── Controls/        ShortcutListControl, SearchBox, KeyComboDisplay (reusable)
├── NativeInterop/   Win32Api (P/Invoke declarations), WindowHookManager
├── Resources/       Styles/, Icons/, DefaultProfiles/
└── Helpers/         SingleInstanceGuard, AnimationHelper
```

## Critical Gotchas

These are the hard-won lessons that save hours of debugging. Read before writing any code.

### 1. Topmost + ShowInTaskbar=False is broken

WPF creates a hidden owner window when `ShowInTaskbar=False`. That owner window does NOT get `WS_EX_TOPMOST`,
so your overlay sinks behind other windows. The fix is to call `SetWindowPos` with `HWND_TOPMOST` via
P/Invoke after the window is created — see `references/win32-interop.md` for the exact code.

### 2. SetWinEventHook callback gets garbage-collected

If you pass a lambda or don't store the delegate reference as a **class field**, the GC collects it and
the hook silently dies (or crashes). Always pin the delegate by storing it in a field on the class that
owns the hook. See the template in `references/win32-interop.md`.

### 3. RegisterHotKey fails silently

Returns `false` if another app already grabbed that key combo — no exception, no error message. Always
check the return value and surface a "hotkey conflict" message to the user with an option to rebind.

### 4. AllowsTransparency disables hardware acceleration for bindings

Layered windows (which `AllowsTransparency=True` forces) fall back to software rendering for data binding
updates. Keep the overlay's binding graph simple — avoid deep nesting, heavy converters, or high-frequency
property changes.

### 5. Thread affinity for Win32 hooks

Both `SetWinEventHook` and `RegisterHotKey` must be called on the UI thread (a thread with a message loop).
Call them from `SourceInitialized` or use `Dispatcher.BeginInvoke` if registering later. Background threads
will silently fail.

### 6. ApplicationFrameHost hides UWP apps

UWP apps (Calculator, Settings, Store) all report as `ApplicationFrameHost.exe`. You need to enumerate
child processes to find the real app — see `references/win32-interop.md` for the pattern.

### 7. Mica/Acrylic requires Windows 11 build 22523+

`DWMWA_SYSTEMBACKDROP_TYPE` doesn't exist on older builds. Check `Environment.OSVersion` at startup and
fall back to a solid semi-transparent brush on Win10.

### 8. DPI scaling causes blurry overlays

Without per-monitor DPI awareness declared in the app manifest, Windows bitmap-scales your overlay when
it moves between monitors. Add `<dpiAwareness>PerMonitorV2</dpiAwareness>` to `app.manifest`.

### 9. Alt+Tab visibility

An overlay with `WindowStyle=None` and `ShowInTaskbar=False` still shows up in Alt+Tab. Use
`WS_EX_TOOLWINDOW` extended style via P/Invoke to hide it, or set `WindowStyle=ToolWindow`
(but that breaks some other things — P/Invoke is cleaner).

### 10. System.Text.Json can't serialize WPF types

`Brush`, `Thickness`, `Point`, etc. aren't serializable. Create plain DTO classes for settings/profiles
and map to/from WPF types in the service layer.

## Code Templates

Detailed, ready-to-use code templates are in the `references/` directory:

- **`references/win32-interop.md`** — All P/Invoke declarations, SetWinEventHook setup,
  GetForegroundWindow + process identification, RegisterHotKey integration, UWP detection,
  SetWindowPos for proper Topmost behavior

- **`references/mvvm-patterns.md`** — CommunityToolkit.Mvvm setup, DI container configuration,
  MainViewModel with profile switching, IOverlayMode interface, ObservableProperty and RelayCommand usage

- **`references/overlay-windows.md`** — XAML templates for all three display modes, transparency setup,
  Mica/Acrylic backdrop code, animation helpers, ShortcutListControl, per-monitor DPI manifest

Read the relevant reference file before writing code for that area. The templates are designed to work
together — they share the same naming conventions, DI patterns, and interface contracts.

## NuGet Dependencies

| Package | Version | Purpose |
|---|---|---|
| `CommunityToolkit.Mvvm` | latest | MVVM base classes with source generators |
| `Microsoft.Extensions.DependencyInjection` | latest | DI container |
| `Hardcodet.NotifyIcon.Wpf` | 2.x | System tray icon |
| `Microsoft.Xaml.Behaviors.Wpf` | latest | Blend behaviors for drag, auto-hide |

No Electron, no Node.js. Pure .NET 8 — target ~25–40 MB memory.

## JSON Profile Schema

Each application gets a JSON file in `%AppData%/ShortcutOverlay/profiles/`:

```json
{
  "profileId": "chrome",
  "displayName": "Google Chrome",
  "processNames": ["chrome"],
  "windowClasses": [],
  "icon": "chrome.png",
  "categories": [
    {
      "name": "Tabs",
      "shortcuts": [
        { "keys": "Ctrl+T", "description": "New tab", "tags": ["tab"] }
      ]
    }
  ]
}
```

`processNames` is an array because some apps use different process names across versions
(e.g., Substance Painter: `"Adobe Substance 3D Painter"`, `"painter"`). `windowClasses` is optional
and used for special cases like Desktop detection (`Progman`, `WorkerW`).

## Special Window Detection Cases

| Scenario | Detection Method |
|---|---|
| **Desktop** | Process is `explorer`, window class is `Progman` or `WorkerW` |
| **File Explorer** | Process is `explorer`, window class is `CabinetWClass` |
| **UWP apps** | Process is `ApplicationFrameHost` → enumerate child processes |
| **Windows Terminal** | Process is `WindowsTerminal` → read title bar or console API for shell type |
| **Unreal Engine** | Process is `UnrealEditor` (may also be `UE4Editor` for older versions) |

## Coding Conventions

- Use `CommunityToolkit.Mvvm` source generators (`[ObservableProperty]`, `[RelayCommand]`)
  instead of manual `INotifyPropertyChanged`
- All services are registered as singletons in the DI container
- ViewModels receive services via constructor injection
- Win32 API calls live exclusively in `NativeInterop/` — never scatter P/Invoke across the codebase
- Profiles are immutable data objects; editing creates a new instance
- Use `async/await` for file I/O but keep Win32 hook callbacks synchronous (they're on the UI thread)
- Name files to match their primary class: `WindowDetectionService.cs` contains `WindowDetectionService`
