# ComeOnOver Desktop Launcher — Roadmap

## v1.0 — Current
- [x] Launch one or more Claude Desktop instances simultaneously
- [x] Fixed named slots to preserve login sessions between launches
- [x] Open ComeOnOver web app (https://comeonover.netlify.app) from launcher
- [x] Settings persist between sessions (slot count)
- [x] Claude install auto-detection (MSIX/WindowsApps + PowerShell fallback)
- [x] Resizable window with sensible minimum size

## v1.1 — Polish
- [ ] System tray icon — minimize to tray, right-click quick-launch menu
- [ ] Auto-update: on launch, re-detect Claude install path in case of updates
- [ ] Show currently running Claude instance count
- [ ] Per-slot status indicators (running / not running)
- [ ] Better error messaging when Claude is not found

## v1.2 — ComeOnOver Integration
- [ ] Native ComeOnOver desktop app detection and launch (when available)
- [ ] Link to ComeOnOver download page if not installed
- [ ] ComeOnOver version display

## v2.0 — Cross-Platform
- [ ] macOS support (Claude Desktop path resolver)
- [ ] Linux support (Claude Desktop AppImage/deb path resolver)
- [ ] Platform-specific path resolver implementations behind IClaudePathResolver
- [ ] CI/CD pipeline for multi-platform builds

## Backlog / Under Consideration
- [ ] Monetisation strategy (GitHub Sponsors / Ko-fi — no ads in-app)
- [ ] Auto-update mechanism for the launcher itself (Squirrel.Windows / Sparkle)
- [ ] Submit to awesome-avalonia list
- [ ] Reddit / HN launch post
- [ ] Slot naming — let users name their slots (e.g. "Work", "Personal")
- [ ] Launch on Windows startup option
- [ ] Configurable ComeOnOver URL (for local dev environments)
