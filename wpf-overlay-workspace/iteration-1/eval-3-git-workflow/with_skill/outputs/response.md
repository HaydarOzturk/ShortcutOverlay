# Git Workflow Response: Saving HotkeyService & ProfileManager

## Saving Your Work

You've completed two critical core foundation components. Here's how to save your work following the git-workflow skill:

### Stage and Commit Your Changes

```bash
# First, check what's changed
git status
git diff --stat

# Stage the modified files
git add src/Services/HotkeyService.cs src/Services/ProfileManager.cs

# Commit with a conventional message
git commit -m "feat(hotkey,profiles): implement HotkeyService and enhance ProfileManager

- HotkeyService: Global hotkey registration and handling with cleanup
- ProfileManager: Completed process-to-profile mapping logic
- Both services ready for integration with detection and overlay layers"

# Push to remote
git push origin phase-1/core-foundation
```

### Why These Commits?

Per the skill's convention, both services fall under the **core foundation phase** and should be tracked as features:
- **`feat(hotkey)`** — HotkeyService is new functionality for the hotkey scope
- **`feat(profiles)`** — ProfileManager enhancements are part of the profiles scope

The granular scope names (hotkey, profiles) map directly to the project's service architecture, making it easy to track what was implemented.

---

## What's Next: Completing Phase 1

You've completed two major components, but Phase 1 (Core Foundation) still requires:

1. **WindowDetectionService** — Active window detection using SetWinEventHook
2. **FloatingWidgetWindow** — Basic overlay UI to display shortcuts
3. **Auto-switching logic** — Tie detection → ProfileManager → Widget updates together

### After Phase 1 is Complete

Once all Phase 1 components are done and tested:

```bash
# Push any final work
git push origin phase-1/core-foundation

# Create a pull request for review
gh pr create \
  --title "Phase 1: Core Foundation" \
  --body "## Summary
- WindowDetectionService with SetWinEventHook
- ShortcutProfile data models and JSON persistence
- ProfileManager with process-to-profile mapping
- HotkeyService with global hotkey registration
- Basic floating widget overlay
- Auto-switching on window change

## Testing
- [ ] Active window detection works for explorer, chrome, cmd
- [ ] Profile JSON loads correctly
- [ ] Floating widget shows and hides
- [ ] Profile switches when active window changes
- [ ] Hotkeys are registered and functional"

# After review/approval, merge into main
gh pr merge --squash

# Update local main and tag the release
git checkout main
git pull origin main
git tag -a v0.1.0 -m "Phase 1: Core Foundation complete"
git push origin v0.1.0
```

### Phase 2: Display Modes

Once Phase 1 merges, you'll move to **phase-2/display-modes**:

```bash
git checkout main
git pull origin main
git checkout -b phase-2/display-modes
git push -u origin phase-2/display-modes
```

Phase 2 focuses on advanced overlay modes (fullscreen overlays, docking, etc.).

---

## Summary

✓ **Immediately:** Run the commit commands above to save HotkeyService and ProfileManager.

✓ **Next:** Complete the remaining Phase 1 components (detection, widget, auto-switching).

✓ **Then:** Create a PR, merge to main, tag v0.1.0, and start Phase 2.

The skill ensures every code change is tracked, pushed frequently, and organized by phase. You're on track!
