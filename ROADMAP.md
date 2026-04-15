# ComeOnOver Desktop Launcher — Roadmap

## v1.0 — Released
- [x] Launch one or more Claude Desktop instances simultaneously
- [x] Fixed named slots to preserve login sessions between launches
- [x] Open ComeOnOver web app (https://comeonover.netlify.app) from launcher
- [x] Settings persist between sessions (slot count)
- [x] Claude install auto-detection (MSIX/WindowsApps + PowerShell fallback)
- [x] Resizable window with sensible minimum size
- [x] Auto-update path cache on every launch (handles Claude updates silently)
- [x] GitHub Actions release pipeline — self-contained .exe and .zip, no .NET required

## v1.1 — Released
- [x] System tray icon — minimize to tray, right-click quick-launch menu
- [x] Close-to-tray — clicking X hides the window, use tray Quit to fully exit
- [x] Running instance count with refresh button
- [x] Fix: windowed process count (no longer inflated by Electron child processes)

## v1.2 — Resource Monitor
- [ ] Per-instance resource display: CPU %, RAM usage, process uptime
- [ ] Combined totals: total CPU and RAM across all running Claude instances
- [ ] Resource data refreshes on a configurable interval (e.g. every 5 seconds)
- [ ] Visual indicator when an instance is using unusually high resources
- [ ] Note: longer conversations consume more memory — this makes it visible

## v1.3 — ComeOnOver Integration
- [ ] Native ComeOnOver desktop app detection and launch (when available)
- [ ] Link to ComeOnOver download page if not installed
- [ ] ComeOnOver version display

## v2.0 — Cross-Platform
- [ ] macOS support (Claude Desktop path resolver)
- [ ] Linux support (Claude Desktop AppImage/deb path resolver)
- [ ] Platform-specific path resolver implementations behind IClaudePathResolver
- [ ] CI/CD pipeline for multi-platform builds

## Monetisation
- [ ] In-app advertising (tasteful, non-intrusive — planned for a future version)
- [ ] GitHub Sponsors / Ko-fi as an alternative for users who prefer ad-free
- [ ] Ads will never appear in v1.x — planned for a later major version once the user base is established

## Backlog / Under Consideration
- [ ] Auto-update mechanism for the launcher itself (Squirrel.Windows / Sparkle)
- [ ] Submit to awesome-avalonia list
- [ ] Reddit / HN launch post
- [ ] Slot naming — let users name their slots (e.g. "Work", "Personal")
- [ ] Launch on Windows startup option
- [ ] Configurable ComeOnOver URL (for local dev environments)
