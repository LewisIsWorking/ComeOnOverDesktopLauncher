# ComeOnOver Desktop Launcher - Roadmap

Current and upcoming work. Historical release notes for v1.0-v1.8.x live in [`docs/RELEASE-HISTORY.md`](docs/RELEASE-HISTORY.md).

## v1.10.2 - Released

User-visible fix for the Start Menu shortcut regression observed during the first real-world v1.10.0 → v1.10.1 auto-update. Velopack 0.0.1298's update apply logged success for the Start Menu `.lnk` but left the Programs folder empty; Windows Search could not find the app. v1.10.2 self-heals on startup.

### New `IShortcutHealer` service

- [x] **`IShortcutHealer.HealIfMissing()`** runs once at app startup after DI is built, before `MainWindow.Show()`. Branches four ways:
  - **`SkippedDevBuild`** — running exe lives outside `%LOCALAPPDATA%\ComeOnOverDesktopLauncher\current\`; nothing to heal. No disk touch.
  - **`AlreadyPresent`** — expected shortcut exists; no-op.
  - **`HealedMissing`** — shortcut was missing, recreated successfully via WScript.Shell COM.
  - **`Failed`** — shortcut was missing and recreation failed; logged, launcher starts normally anyway (non-blocking).
- [x] **`IShellLinkWriter`** abstraction over the COM API means the healer is unit-testable without real COM. `WScriptShellLinkWriter` is the production implementation; tests substitute an `NSubstitute` mock.
- [x] **Testable branching** via two injected delegates: `Func<string> getRunningExePath` and `Func<string, bool> fileExists`. Unit tests drive every result path deterministically.
- [x] **6 new tests** in `WindowsShortcutHealerTests`: dev-build skip, shortcut-present no-op, shortcut-missing-heals, shortcut-missing-writer-fails, case-insensitive path comparison (Velopack records case-preserving, Windows FS is case-insensitive), probe exception collapses to Failed (never throws out of `HealIfMissing`).

### Why self-heal and not wait for upstream

Velopack 0.0.1298 is the bug's source but a Velopack fix (a) may take weeks, (b) would only reach users on the _next_ update after the fix shipped upstream. The self-heal runs locally on every CoODL launch, so even users currently running a broken v1.10.1 install will see their shortcut restored on next launch after upgrading to v1.10.2. Also defensive against any future recurrence and against users who delete the shortcut themselves — noted tradeoff but low-cost to restore.

### CA1416 test fix

- [x] Annotated `WindowsShortcutHealerTests` with `[SupportedOSPlatform("windows")]`. Per the never-suppress rule, fixing the CA1416 warnings at the root rather than with `NoWarn` - the test class genuinely _is_ Windows-only.

### Numbers

- 253 tests passing (247 + 6 new). 0 warnings, 0 errors.
- 5 files added (`IShortcutHealer`, `IShellLinkWriter`, `WScriptShellLinkWriter`, `WindowsShortcutHealer`, `WindowsShortcutHealerTests`). 1 file modified (`App.axaml.cs`: DI + startup call).
- All files ≤200 lines.
- Local `vpk pack` unchanged (no CI changes, no new Velopack behaviour).

### Observed end-to-end (2026-04-20)

- [x] v1.10.1 -> v1.10.2 auto-update delivered the delta package (89 KB, not the full 49 MB).
- [x] After the update applied, v1.10.2 launched and logged `[HealIfMissing] Shortcut heal: already present` - the healer's `AlreadyPresent` branch fired correctly because the shortcut was manually restored earlier in the session. A fresh install that hits the Velopack shortcut bug would instead log `Shortcut heal: missing at ... - recreating` followed by `Created shortcut`.
- [x] First Velopack delta package in the project's history (see also the `vpk download github` step added in commit `6eae3db` - it kicked in on this second release).

## v1.10.1 - Released

Validation hotfix for the v1.10.0 Velopack migration. Ships a one-line log-message enrichment purely so the auto-update pipeline can be exercised end-to-end: v1.10.0 users should receive this release automatically on their next poll tick (up to 6 hours) or next launcher restart.

### The one-line change

- [x] **`VelopackAutoUpdateService.ApplyUpdatesAndRestart` log line** now reports both source and target versions (`current X.Y.Z -> target A.B.C`) instead of just the target. Diagnostic improvement: when reading the log after an update fails or behaves oddly, you can now see at a glance which version the user was running when they clicked "Restart to install".

### What this release validates

- [x] **Second CI run with the fixed `--token` workflow produces a working release**. v1.10.0's initial CI attempt failed due to a token-passing bug; v1.10.1 confirms the fixed workflow is stable across runs (1m52s CI time, release auto-published with all expected assets).
- [ ] **Delta package generation** did NOT work as expected. v1.10.1 shipped a `-full.nupkg` but not a `-delta.nupkg`, despite `fetch-depth: 0` on checkout. Root cause: Velopack generates deltas by diffing against the **previous release's actual `.nupkg` content**, not from git history - the previous nupkg needs to be on disk at pack time. The fix (`vpk download github` before `vpk pack`) is queued for the next release and documented in `docs/dev/VELOPACK.md`.
- [x] **Install + update round-trip observed end-to-end on a real machine** (2026-04-20). v1.10.0 Setup.exe installed cleanly via `--silent`, all 6 design decisions confirmed in practice (install location, shortcuts, Add/Remove Programs entry, auto-update default ON, silent download, restart-to-install button). The launcher went v1.10.0 → auto-detected v1.10.1 in 2s → downloaded full .nupkg in 2s → restart applied cleanly → v1.10.1 running within 32 seconds of first launch, zero user input required beyond clicking the banner. Full post-install log preserved in the session transcript.

### No other changes

Deliberately trivial. If anything else in the launcher behaviour or UI changes between v1.10.0 and v1.10.1, this release has failed at its job (which is to isolate the update-pipeline validation from any other variable).

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
- [ ] **Shortcut icon cache refresh after heal** — when `IShortcutHealer` recreates a missing .lnk (v1.10.2+ behaviour), Windows' icon cache may still show the previous broken state until the user triggers a refresh or the cache expires naturally. Observed 2026-04-20 on Lewis's machine after the initial heal. The fix is to invoke `ie4uinit.exe -show` after a successful heal (Microsoft-documented icon-cache refresh, lighter than killing explorer.exe). Alternatively the heavier `Stop-Process explorer; Remove-Item iconcache*.db; Start-Process explorer.exe` sequence works but flashes the taskbar. Should probably be a new `IIconCacheRefresher` service injected into `WindowsShortcutHealer` so the refresh only happens on the `HealedMissing` branch and never on `AlreadyPresent`. ~30 lines of code; plan for v1.10.3 or folded into v1.11.

- [ ] **Show/hide button per slot** — a per-card button that toggles the associated Claude window between visible and hidden (close-to-tray) state. Motivation: right now the only way to hide a slot is to minimise-to-tray via Claude itself, and the only way to bring it back is to find it in the Windows system tray icon stack. CoODL already detects tray-resident slots (v1.8.2 surfaced them in the "Hidden / tray" section), so it knows which slots are hidden vs visible — a button that calls the right Win32 API to flip that state closes the loop. Implementation sketch: `ShowWindow(hwnd, SW_HIDE)` / `ShowWindow(hwnd, SW_SHOW) + SetForegroundWindow(hwnd)`. Button icon/label should reflect current state ("Hide" when visible, "Show" when tray-resident). Each slot row and each tray-resident row gets the button — one UI affordance that works in both directions. Risk: Claude's main-window Hwnd can change across minimise/restore cycles; the scanner might need to re-discover it. Test before shipping.

- [ ] **Shared extension store across slots** — each `ClaudeSlot{N}\Claude Extensions\` is currently a separate copy of the extension tree, so installing a 2 GB extension like Desktop Commander N times burns N × 2 GB of disk and N × download time. CoODL could maintain a single shared `%LOCALAPPDATA%\ComeOnOverDesktopLauncher\extensions\{extension-id}\` store and, on slot initialisation, create junction points (symlinks) from each slot's `Claude Extensions\{extension-id}\` into the shared store. Claude would see a normal-looking folder; changes written via one slot would propagate to all. Risks: per-slot configuration divergence if an extension writes config into its own install dir (most don't — config typically lives in the slot's `claude_desktop_config.json`), and Claude might not handle the junction points gracefully on auto-update (need to test). Worth a spike-and-evaluate investigation.

- [ ] **Per-slot activity preview** — surface what each slot is doing at a glance so you don't have to alt-tab to remember. Open design questions before building:
    - **Thumbnail approach** (visual): periodically capture each slot's window via `PrintWindow`, downscale to 160x120, show under each row. Privacy win: no content parsing. Privacy loss: any text on the capture is readable if someone shoulder-surfs the launcher. Cost: ~20 ms per slot per capture, one capture every 30-60 s is plenty.
    - **Metadata approach** (textual): tail the latest message or active-tool indicator from Claude's local state store. Lower visual density but more useful at tiny sizes. Privacy concern much worse (the launcher would be reading conversation content), would need strict opt-in. Also depends on Claude's storage format being stable, which it isn't.
    - **Activity-signal approach** (minimal): show renderer CPU %, node-service CPU % (MCP activity), and last-interacted timestamp per slot. Zero content exposure, fast, survives Claude storage-format changes. Less "at a glance what is Slot 3 doing" but answers "is Slot 3 busy?" which is the common need.
    - Likely lands as a combo: activity signal always visible + optional thumbnail behind a settings toggle (off by default). Thumbnails stored in-memory only, never written to disk. Would want a "pause captures while on battery" option for laptop users.

- [ ] Auto-update mechanism for the launcher itself (Squirrel.Windows / Sparkle)
- [ ] Submit to awesome-avalonia list
- [ ] Reddit / HN launch post
