---
name: git-workflow
description: >
  Git and GitHub version control workflow for the ShortcutOverlay project. Use this skill whenever
  working on code changes, completing implementation phases, creating branches, committing work,
  pushing to remote, creating pull requests, tagging releases, or any git-related task. Also trigger
  when the user asks about version control, branching strategy, or project tracking. This skill
  ensures Claude always provides pull/push commands when appropriate, tracks project stages with
  proper commits, and follows a structured branching strategy. Use this even if the user doesn't
  explicitly mention git — any time code is written or modified, version control should be part
  of the workflow.
---

# Git & GitHub Workflow — ShortcutOverlay Project

This skill ensures every code change is properly tracked, committed, pushed, and organized
throughout the ShortcutOverlay project lifecycle. The user wants rigorous version control at
every stage — never let code changes go uncommitted or unpushed.

## Core Rules

1. **Always remind about git** — After writing or modifying code, provide the git commands
   to stage, commit, push, and (if needed) create a PR. Don't wait for the user to ask.

2. **Track every stage** — Each implementation phase from the project plan gets its own
   branch. Commits within a phase should be granular and well-described.

3. **Push frequently** — After commits, always suggest pushing to remote. Code that only
   exists locally is at risk.

4. **Use conventional commits** — Keep commit messages consistent and scannable.

## Repository Setup

When starting the project for the first time:

```bash
# Initialize the repository
git init ShortcutOverlay
cd ShortcutOverlay

# Create .gitignore for .NET/C# projects
cat > .gitignore << 'EOF'
## .NET / Visual Studio
bin/
obj/
*.user
*.suo
*.vs/
*.DotSettings.user
packages/
*.nupkg

## Build outputs
[Dd]ebug/
[Rr]elease/
x64/
x86/

## IDE
.idea/
.vscode/
*.swp

## OS
Thumbs.db
.DS_Store

## User settings
appsettings.Development.json
EOF

# Initial commit
git add .gitignore
git commit -m "chore: initialize repository with .gitignore"

# Create GitHub repository and push
gh repo create ShortcutOverlay --private --source=. --push

# Set up branch protection on main (optional but recommended)
git branch -M main
git push -u origin main
```

## Branching Strategy

Use a feature-branch workflow aligned with the project plan phases:

```
main                          ← stable, releasable
├── phase-1/core-foundation   ← Week 1-2 work
├── phase-2/display-modes     ← Week 3 work
├── phase-3/settings-editor   ← Week 4 work
├── phase-4/polish            ← Week 5 work
└── phase-5/advanced          ← Week 6+ work
```

**Branch naming:** `phase-N/short-description` for phase work, `fix/description` for bugs,
`feat/description` for features within a phase.

### Starting a new phase

```bash
# Make sure main is up to date
git checkout main
git pull origin main

# Create the phase branch
git checkout -b phase-1/core-foundation
git push -u origin phase-1/core-foundation
```

### Finishing a phase

```bash
# Push all remaining work
git push origin phase-1/core-foundation

# Create a pull request for review
gh pr create \
  --title "Phase 1: Core Foundation" \
  --body "## Summary
- WindowDetectionService with SetWinEventHook
- ShortcutProfile data models and JSON persistence
- ProfileManager with process-to-profile mapping
- Basic floating widget overlay
- Auto-switching on window change

## Testing
- [ ] Active window detection works for explorer, chrome, cmd
- [ ] Profile JSON loads correctly
- [ ] Floating widget shows and hides
- [ ] Profile switches when active window changes"

# After review, merge into main
gh pr merge --squash

# Update local main
git checkout main
git pull origin main
```

## Commit Convention

Use [Conventional Commits](https://www.conventionalcommits.org/) with scopes matching
the project's service/component areas:

### Format
```
type(scope): description

[optional body with details]
```

### Types
| Type | When to use |
|---|---|
| `feat` | New feature or functionality |
| `fix` | Bug fix |
| `refactor` | Code restructuring without behavior change |
| `style` | XAML/CSS styling, formatting |
| `chore` | Build config, dependencies, tooling |
| `docs` | Documentation, comments, README |
| `test` | Adding or updating tests |

### Scopes
| Scope | Area |
|---|---|
| `detection` | WindowDetectionService, Win32 hooks |
| `profiles` | ProfileManager, JSON profiles |
| `overlay` | Any overlay window (floating, panel, tray) |
| `hotkey` | HotkeyService, global hotkey registration |
| `settings` | SettingsService, preferences UI |
| `tray` | TrayIconService, system tray |
| `models` | Data models (ShortcutProfile, etc.) |
| `ui` | General UI, styles, themes, controls |
| `interop` | NativeInterop, P/Invoke |

### Examples

```bash
# Adding a new service
git commit -m "feat(detection): implement WindowDetectionService with SetWinEventHook

Uses EVENT_SYSTEM_FOREGROUND for event-driven detection.
Handles special cases: Desktop, File Explorer, UWP apps."

# Fixing a bug
git commit -m "fix(detection): pin WinEventHook delegate to prevent GC collection

The callback delegate was getting garbage-collected, causing
the hook to silently stop working after ~30 seconds."

# Adding UI
git commit -m "feat(overlay): create FloatingWidgetWindow with shortcut list

Includes drag support, transparency, and Topmost fix via SetWindowPos."

# Dependency update
git commit -m "chore: add CommunityToolkit.Mvvm and DI packages"
```

## After Every Code Session

At the end of every coding session (or after completing a meaningful chunk of work),
Claude should provide these commands:

```bash
# Check what's changed
git status
git diff --stat

# Stage and commit (adjust files and message as needed)
git add <specific-files>
git commit -m "type(scope): description"

# Push to remote
git push origin <current-branch>
```

If there are uncommitted changes and the user seems to be wrapping up, proactively
remind them:

> "You have uncommitted changes. Here are the git commands to save your work:
> ```bash
> git add src/Services/WindowDetectionService.cs
> git commit -m 'feat(detection): add UWP app child process enumeration'
> git push origin phase-1/core-foundation
> ```"

## Tagging Releases

When a phase is complete and merged to main, tag it:

```bash
git checkout main
git pull origin main
git tag -a v0.1.0 -m "Phase 1: Core Foundation complete"
git push origin v0.1.0
```

### Version scheme
| Version | Meaning |
|---|---|
| `v0.1.0` | Phase 1 complete |
| `v0.2.0` | Phase 2 complete |
| `v0.3.0` | Phase 3 complete |
| `v0.4.0` | Phase 4 complete (first "polished" build) |
| `v0.5.0` | Phase 5 features |
| `v1.0.0` | First public release |

## Common Operations Quick Reference

```bash
# See what branch you're on and its status
git status

# See recent commits
git log --oneline -10

# Undo last commit (keep changes staged)
git reset --soft HEAD~1

# Stash work in progress
git stash push -m "WIP: description"
git stash pop

# See diff of what you're about to commit
git diff --cached

# Rebase your branch on latest main
git fetch origin
git rebase origin/main

# Create a PR from command line
gh pr create --title "title" --body "description"

# Check CI status
gh pr checks

# View open PRs
gh pr list
```

## GitHub Issues (Optional)

If the user wants to track tasks as issues:

```bash
# Create issues for each phase task
gh issue create --title "Implement WindowDetectionService" \
  --body "Set up SetWinEventHook and process identification" \
  --label "phase-1"

# Reference issues in commits
git commit -m "feat(detection): implement WindowDetectionService

Closes #1"

# List open issues
gh issue list
```
