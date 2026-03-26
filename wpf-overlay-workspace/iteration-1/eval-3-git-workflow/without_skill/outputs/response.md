# Git Workflow for ShortcutOverlay - Core Foundation Phase

## Saving Your Work (Committing Changes)

You've completed two key services in the core foundation phase. Here's how to save your work:

### 1. Stage Your Changes

```bash
git add src/Services/HotkeyService.cs src/Services/ProfileManager.cs
```

Or to see what's staged first:

```bash
git status
```

### 2. Create a Meaningful Commit

```bash
git commit -m "Implement HotkeyService and ProfileManager

- Add HotkeyService for keyboard shortcut detection and handling
- Implement ProfileManager for configuration management
- Complete core foundation phase services"
```

**Commit Message Best Practices:**
- First line: Short summary (50 characters or less)
- Blank line
- Detailed description of what and why (not just what)
- Reference any related issues: `Closes #123`

### 3. Verify Your Commit

```bash
git log --oneline -5
```

## Next Steps After Core Foundation Phase

### A. Before Merging to Main

1. **Ensure Code Quality**
   - Run unit tests for HotkeyService and ProfileManager
   - Code review - have a teammate review these implementations
   - Verify no breaking changes to existing code

2. **Update Documentation**
   - Add XML comments to public methods
   - Update README.md if there are new dependencies or setup requirements
   - Document the architecture decisions

3. **Final Testing**
   ```bash
   # Build and verify no warnings
   dotnet build

   # Run all tests
   dotnet test
   ```

### B. Merging Back to Main

```bash
# Switch to main branch
git checkout main

# Ensure main is up to date
git pull origin main

# Merge your feature branch
git merge phase-1/core-foundation

# Or use squash merge if you want a clean history
git merge --squash phase-1/core-foundation
git commit -m "Merge core foundation phase: HotkeyService and ProfileManager"
```

### C. Create a Release Tag (Optional but Recommended)

```bash
git tag -a v0.1.0-foundation -m "Core foundation phase complete: HotkeyService, ProfileManager"
git push origin v0.1.0-foundation
```

### D. Planning Next Phase

After completing core foundation, typical next phases are:

1. **UI Implementation** (phase-2/ui-framework)
   - WPF window overlays
   - Hotkey binding UI
   - Profile management UI

2. **Integration** (phase-3/integration)
   - Connect HotkeyService to UI
   - Connect ProfileManager to persistence
   - End-to-end testing

3. **Polish** (phase-4/refinement)
   - Performance optimization
   - Error handling
   - User experience improvements

## Quick Git Workflow Summary

```bash
# Current state
git status                              # See what's changed
git diff src/Services/                  # Review changes before committing

# Save your work
git add src/Services/HotkeyService.cs src/Services/ProfileManager.cs
git commit -m "Your commit message"

# Prepare to merge
git fetch origin                        # Get latest from remote
git rebase origin/main                  # Optional: keep history clean

# Merge to main
git checkout main
git merge phase-1/core-foundation
git push origin main
```

## Important Notes

- **Always pull before pushing**: Prevents merge conflicts
- **Commit frequently**: Smaller, logical commits are easier to review and debug
- **Write descriptive messages**: Your future self will thank you
- **Test before committing**: Avoid committing broken code
- **Use branches for features**: Keep main stable for production/releases

---

**Your current context:**
- Project: ShortcutOverlay (C# WPF)
- Branch: `phase-1/core-foundation`
- Files changed: `src/Services/HotkeyService.cs`, `src/Services/ProfileManager.cs`
- Phase status: Core foundation complete, ready for next phase
