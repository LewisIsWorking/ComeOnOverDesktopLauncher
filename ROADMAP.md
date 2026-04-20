# ComeOnOver Desktop Launcher - Roadmap

Current and upcoming work. Historical release notes for v1.0-v1.8.x live in [`docs/RELEASE-HISTORY.md`](docs/RELEASE-HISTORY.md).

## v1.10.0 - Released

> ⚠️ **Migration notice.** v1.10.0 switches CoODL from a portable single-exe distribution to a Velopack-based installer with background auto-update. Existing users must download `ComeOnOverDesktopLauncher-win-Setup.exe` once from the [v1.10.0 release page](https://github.com/LewisIsWorking/ComeOnOverDesktopLauncher/releases/tag/v1.10.0); from v1.10.1 onwards updates apply automatically. See [`docs/MIGRATION.md`](docs/MIGRATION.md) for step-by-step migration notes. Settings, slot nicknames, login sessions and Claude slot data all persist unchanged.

The plumbing release. No new end-user features; instead, the entire distribution and update story gets rebuilt on top of [Velopack](https://velopack.io) so future releases can reach users without a manual download every time.

### Velopack integration

- [x] **`Velopack` 0.0.1298 package reference** added. Velopack is .NET-native, actively maintained (latest SDK Feb 2026), and the modern successor to Squirrel.Windows. Runs in Rust for native-fast pack/update performance.
- [x] **`VelopackApp.Build().OnFirstRun(...).Run()` as the first line of `Program.Main`** — before Avalonia boots. Velopack's install/update/uninstall hooks launch our exe with special CLI arguments; the `Run()` call intercepts them and short-circuits before any Avalonia window would appear. Placing this call anywhere later triggers a loud warning from `vpk pack` and causes hook UX to stutter.
- [x] **New `IAutoUpdateService` interface** + `VelopackAutoUpdateService` implementation. Wraps Velopack's concrete `UpdateManager` behind our standard `I<Service>` abstraction so VMs depend on an interface and tests can substitute. `GithubSource` wired to the CoODL releases repo. Dev-build guard returns `NoUpdateAvailable` when `!IsInstalled` so `dotnet run` never hits the network.
- [x] **Logging on every service call** — `CheckForUpdatesAsync`, `DownloadUpdatesAsync`, and `ApplyUpdatesAndRestart` each emit info-level log lines with the relevant state. Per the permanent-logging hard rule.

### Install + update UX (the 6 locked design decisions)

- [x] **v1.10.0 is the cutover** (decision 1-A). Not a parallel portable-plus-installer transitional release — single clean switch. Release notes explain the one-time migration.
- [x] **Installs to `%LOCALAPPDATA%\ComeOnOverDesktopLauncher\current\`** (decision 2-a). Velopack default. No UAC prompt on install or updates; per-user install matches modern launcher-style app convention (Discord, Slack, VS Code, Claude Desktop itself).
- [x] **Desktop + Start Menu shortcuts** created at install time (decision 3-c, `--shortcuts "Desktop,StartMenu"`). Users get one-click access from both common surfaces. The "Launch on startup" checkbox in the launcher still owns the startup-folder entry, so there's no collision with Velopack's `Startup` shortcut.
- [x] **Silent download, prompt to restart** (decision 4-b). Background check every 6 hours; when an update is found, silent download with progress shown in a blue banner; when ready, green "Restart to install vX.Y.Z" banner appears. User clicks when convenient. Restart timing matters more than update timing for people mid-Claude-conversation.
- [x] **"Auto-update" checkbox, default ON** (decision 5-a). Persisted in `AppSettings.AutoCheckForUpdates`. Users who prefer manual control can toggle it off; subsequent check ticks are skipped. Same ethos as the existing "Show thumbnails" and "Launch on startup" toggles.
- [x] **No code signing for v1.10.0** (decision 6-a). Users see Windows SmartScreen's "More info" dialog on first install; subsequent auto-updates inherit trust from the already-installed binary and don't re-trigger it. Azure Trusted Signing documented as the future cost-effective signing option in `docs/dev/BUILD-AND-TOOLING.md`.

### State-aware update banner

- [x] **Three mutually-exclusive sub-banners** in `LaunchControlsPanel`:
  - **Blue (Downloading)**: live progress bar + "Downloading v1.10.1... 42%" text. Shows that background work is happening without forcing user attention.
  - **Green (ReadyToInstall)**: "v1.10.1 ready - restart to install" + "What's new" button (opens GitHub release notes) + "Restart to install" button.
  - **Amber (Failed)**: "Update check failed. Will retry automatically later." + "Retry now" button for user-triggered retry.
- [x] **`Update` sub-VM** (`MainWindowUpdateViewModel`) exposes all four states and the three commands. Same pattern as `SlotInstances` and `ExternalInstances` sub-VMs; XAML binds through `Update.IsDownloading`, `Update.RestartCommand` etc.
- [x] **`UpdateOrchestrator` state machine** (`Idle -> Checking -> Downloading -> ReadyToInstall | Failed`) lives as a pure non-UI helper so state transitions are 100% testable without Avalonia. 10 state-machine tests cover every transition and guard.

### Retired

- [x] **`IUpdateNotifier` + `UpdateNotifier`** (the old v1.5 GitHub-releases-polling banner code). Replaced by the Velopack pipeline above.
- [x] **`IUpdateChecker` + `GitHubUpdateChecker`** (the old `HttpClient`-based version comparison).
- [x] **`UpdateNotifierTests` + `GitHubUpdateCheckerTests`** deleted (10 tests total).
- [x] **`HttpClient` DI registration** removed — nothing else in the app uses it.
- Net test count: 237 (Phase 2) + 10 new orchestrator tests = 247. Same as v1.9.2.

### Build pipeline rewrite

- [x] **`.github/workflows/release.yml` rebuilt around `vpk pack` + `vpk upload github`**. Softprops action-gh-release dropped — Velopack creates the GitHub release itself. Key changes: `fetch-depth: 0` on checkout (delta generation needs git history); `PublishSingleFile=false` (Velopack needs multi-file output); version extraction step that strips the `v` prefix from the tag because Velopack rejects it.
- [x] **.NET 10 end-to-end on CI**. The old `net10.0 -> net9.0` regex substitution hack is gone. `actions/setup-dotnet@v4` with `dotnet-version: '10.0.x'` installs the SDK directly — latest SDK 10.0.202 released 2026-04-14, GA support through Nov 2028.
- [x] **Artifacts produced by each release**:
  - `ComeOnOverDesktopLauncher-win-Setup.exe` — the installer end users download
  - `ComeOnOverDesktopLauncher-{version}-full.nupkg` — full Velopack update package
  - `ComeOnOverDesktopLauncher-{version}-delta.nupkg` — incremental diff from previous release (v1.10.1 onwards only)
  - `ComeOnOverDesktopLauncher-win-Portable.zip` — bonus portable variant for users who prefer no installer

### Doc additions

- [x] **`docs/MIGRATION.md`** — step-by-step v1.9.x → v1.10.0 user guide covering Setup.exe download, SmartScreen dismissal, optional "Launch on startup" re-toggle (registry path change), where settings persist across the switch, uninstall procedure, and rollback.
- [x] **`docs/dev/BUILD-AND-TOOLING.md`** gets a full Velopack section — local `vpk pack` commands, the `PublishSingleFile=false` invariant, the `fetch-depth: 0` invariant, the post-ship validation strategy (ship v1.10.1 trivial hotfix to prove auto-update works), code-signing future plans.
- [x] **`docs/dev/LEARNINGS.md`** gets a new architecture invariant entry for Velopack + cross-references to the companion docs.
- [x] **`README.md`** install section rewritten — points at `Setup.exe` rather than the portable .exe, mentions the SmartScreen warning, links to MIGRATION.md, preserves the portable-zip option for users who want it.

### Numbers

- 247 tests passing (237 + 10 new `UpdateOrchestratorTests`). 0 warnings, 0 errors.
- All files ≤200 lines. `MainWindowViewModel` is 197 lines (3 lines of headroom), forced the `MainWindowUpdateViewModel` sub-VM extraction which happens to be the cleanest split anyway.
- Local `vpk pack` produces a 51.8 MB Setup.exe + 49.4 MB full .nupkg + 49.4 MB Portable.zip in ~15 seconds. Warnings about unsigned binaries are expected (decision 6-a).

## v2.0 - ComeOnOver Integration
- [ ] Native ComeOnOver desktop app detection and launch (when available)
- [ ] Link to ComeOnOver download page if not installed
- [ ] ComeOnOver version display

## v3.0 - Cross-Platform
- [ ] macOS support (Claude Desktop path resolver)
- [ ] Linux support (Claude Desktop AppImage/deb path resolver)
- [ ] Platform-specific path resolver implementations behind IClaudePathResolver
- [ ] CI/CD pipeline for multi-platform builds

## Monetisation
- [ ] In-app advertising (tasteful, non-intrusive - planned for a future version)
- [ ] GitHub Sponsors / Ko-fi as an alternative for users who prefer ad-free
- [ ] Ads will never appear in v1.x - planned for a later major version once the user base is established

## Backlog / Under Consideration
- [ ] **Shared extension store across slots** — each `ClaudeSlot{N}\Claude Extensions\` is currently a separate copy of the extension tree, so installing a 2 GB extension like Desktop Commander N times burns N × 2 GB of disk and N × download time. CoODL could maintain a single shared `%LOCALAPPDATA%\ComeOnOverDesktopLauncher\extensions\{extension-id}\` store and, on slot initialisation, create junction points (symlinks) from each slot's `Claude Extensions\{extension-id}\` into the shared store. Claude would see a normal-looking folder; changes written via one slot would propagate to all. Risks: per-slot configuration divergence if an extension writes config into its own install dir (most don't — config typically lives in the slot's `claude_desktop_config.json`), and Claude might not handle the junction points gracefully on auto-update (need to test). Worth a spike-and-evaluate investigation.

- [ ] **Per-slot activity preview** — surface what each slot is doing at a glance so you don't have to alt-tab to remember. Open design questions before building:
    - **Thumbnail approach** (visual): periodically capture each slot's window via `PrintWindow`, downscale to 160x120, show under each row. Privacy win: no content parsing. Privacy loss: any text on the capture is readable if someone shoulder-surfs the launcher. Cost: ~20 ms per slot per capture, one capture every 30-60 s is plenty.
    - **Metadata approach** (textual): tail the latest message or active-tool indicator from Claude's local state store. Lower visual density but more useful at tiny sizes. Privacy concern much worse (the launcher would be reading conversation content), would need strict opt-in. Also depends on Claude's storage format being stable, which it isn't.
    - **Activity-signal approach** (minimal): show renderer CPU %, node-service CPU % (MCP activity), and last-interacted timestamp per slot. Zero content exposure, fast, survives Claude storage-format changes. Less "at a glance what is Slot 3 doing" but answers "is Slot 3 busy?" which is the common need.
    - Likely lands as a combo: activity signal always visible + optional thumbnail behind a settings toggle (off by default). Thumbnails stored in-memory only, never written to disk. Would want a "pause captures while on battery" option for laptop users.

- [ ] Auto-update mechanism for the launcher itself (Squirrel.Windows / Sparkle)
- [ ] Submit to awesome-avalonia list
- [ ] Reddit / HN launch post
