# ComeOnOver Desktop Launcher - Roadmap

Current and upcoming work. Historical release notes:
- [`docs/release-history/v1.0-v1.8.md`](docs/release-history/v1.0-v1.8.md) - foundation + UI maturity era
- [`docs/release-history/v1.9.md`](docs/release-history/v1.9.md) - thumbnail-and-card series
- [`docs/release-history/v1.10.md`](docs/release-history/v1.10.md) - Velopack migration through v1.10.3 icon-cache polish
- [`docs/RELEASE-HISTORY.md`](docs/RELEASE-HISTORY.md) - index pointing at the above

## v1.10.6 - Released
Completes the show/hide-button pair introduced in v1.10.5. Adds a Show button to every TrayCard row so hidden Claude slots can be restored to the foreground directly from the launcher without digging through Claude system-tray menu. Also fixes a latent bug where tray-resident slots were invisible to the resource monitor and were dropped from both collections on the next poll.
### New IWindowShower service
- IWindowShower.TryShow(int processId) enumerates all top-level windows via EnumWindows + GetWindowThreadProcessId, selects the best candidate for the target PID, then calls ShowWindow(hwnd, SW_SHOW) and SetForegroundWindow(hwnd). Returns true on success, false on any failure. Never throws.
- Window selection heuristic: skip WS_EX_TOOLWINDOW windows (Electron renderer helpers), prefer windows with a non-empty title (the main Electron browser window always has a page title), fall back to first remaining match.
- Managed EnumWindowsProc delegate held in a local variable during enumeration to prevent GC collection mid-callback.
- SetForegroundWindow can fail silently due to Windows foreground-lock mechanism. Logged but not fought.
- Separate interface from IWindowHider (single-responsibility: different Win32 footprint, different failure modes).
### Tray-resident slot visibility fix
- SlotInstanceListViewModel.Refresh synthesises stub InstanceResourceSnapshot entries for tray-resident PIDs that have no resource snapshot (the resource monitor only snapshots windowed processes). Stubs carry real uptime from ClaudeProcessInfo.StartTime; CPU/RAM show zero.
- MainWindowResourceViewModel derives RunningInstanceCount from collection sizes after reconciliation (Items.Count + TrayItems.Count + ExternalInstances.Count) instead of IClaudeInstanceLauncher.GetRunningInstanceCount() which only counted windowed processes.
- MainWindowResourceViewModel no longer depends on IClaudeInstanceLauncher.
### UI: Show button on TrayCard
- Teal (hex 80CBC4) Show button in column 3 of the TrayCard stats row. Quit moves to column 4.
- ClaudeInstanceViewModel.ShowCommand routes through a new OnShow callback, wired by SlotCallbackBinder to IWindowShower.TryShow.
### Numbers
- 263 tests passing. 0 warnings, 0 errors.
- 2 files added (IWindowShower, Win32WindowShower). 7 files modified + 3 test files updated.
- All files <=200 lines.
## v1.10.5 - Released

Adds a Hide button to every slot card so users can close a Claude slot to the system tray without terminating the process. Closes half of the show/hide-button backlog item; the Show-from-tray side remains deferred (requires enumerating hidden top-level windows by PID, which is more complex than Hide).

### New `IWindowHider` service

- `IWindowHider.TryHide(int processId)` - resolves the process's `MainWindowHandle` and calls `ShowWindow(hwnd, SW_HIDE)`. Returns `true` on success, `false` on any failure (process gone, zero hwnd, Win32 call rejected). Never throws.
- `Win32WindowHider` is the production implementation using `user32.dll` P/Invoke. `SW_HIDE` (not `SW_MINIMIZE`) - minimise leaves the window in the taskbar, hide removes it entirely, matching the user's mental model of "close to tray".
- Logged on every invocation so diagnostic tail-reads can see whether Hide attempts landed.

### UI: Hide button on `SlotCard`

- Button sits in the stats row between the uptime display and the existing Kill (X) button. Neutral grey (`#A0A0A0`) foreground so it visually de-emphasises vs Kill's red (`#EF9A9A`) - the non-destructive action is less visually weighted than the destructive one.
- Tooltip explains the behaviour: "Hide this Claude window to the system tray without terminating it. The slot keeps running (MCP connections stay alive) and will reappear in the 'Hidden / tray' section. Reopen it via Claude's own tray icon."
- `ClaudeInstanceViewModel.HideCommand` routes through a new `OnHide` callback (same pattern as existing `OnKill` and `OnShowPreview`), wired up by `SlotCallbackBinder` to call `IWindowHider.TryHide`.
- No refresh prod needed after hide - the next scanner poll (on its own schedule) naturally moves the slot into the TrayCard list via the existing tray-resident classification.

### Numbers

- 263 tests passing. 0 warnings, 0 errors.
- 2 files added (`IWindowHider`, `Win32WindowHider`). 5 files modified (`ClaudeInstanceViewModel`, `SlotInstanceListViewModel`, `SlotCallbackBinder`, `MainWindowViewModel`, `App.axaml.cs`) plus XAML (`SlotCard.axaml`) + test fixture.
- All files <=200 lines. `MainWindowViewModel.cs` is now at 200 (at the limit) - the next change MUST extract to a new sub-VM.
- Win32WindowHider itself has no unit tests - all its logic is P/Invoke + error handling that's more meaningfully validated by live smoke-test than by mocking `user32.dll`. The logging is traceable via the existing app log.

### Deferred to v1.10.6+: Show-from-tray

Showing a hidden window requires enumerating all top-level windows (visible and hidden) to find the one belonging to the target PID - `Process.MainWindowHandle` returns `IntPtr.Zero` for hidden windows, so the same simple lookup Hide uses doesn't work. Needs `EnumWindows` + `GetWindowThreadProcessId` + `ShowWindow(SW_SHOW) + SetForegroundWindow`. Users can still re-show a hidden slot via Claude's own system-tray icon in the meantime; the existing tray-resident detection surfaces hidden slots in the launcher's TrayCard list, closing the loop at the UX level even before the Show button lands.

## v1.10.4 - Released

Surfaces the v1.10.2 -> v1.10.3 apply-failure bug to users so the "restart did nothing" symptom stops being silent. Doesn't fix the underlying Velopack/Defender race condition but ends the confusion of clicking Restart and having the banner reappear as if nothing happened.

### New `IUpdateApplyFailureDetector` service

- Reads the tail of `%LOCALAPPDATA%\ComeOnOverDesktopLauncher\velopack.log` on startup (last 16 KB, fast) and scans for `[ERROR] Apply error:` lines with timestamps within a 2-minute window.
- Never throws - any IO or parse error collapses to `false` (fail closed).
- Handles midnight rollover - Velopack logs only `HH:MM:SS` with no date; tries "today" first, falls back to "yesterday" if today's timestamp would be in the future.
- Regex tested against real production data: correctly matches all 8 apply-error entries from Lewis's machine on 2026-04-20.

### New `UpdateUiState.ApplyFailed` state

- `UpdateOrchestrator.MarkApplyFailed()` transitions the state machine directly. Called once at `MainWindowUpdateViewModel` construction if the detector returns true.
- Distinct from the existing `Failed` state because the UI actions differ: ApplyFailed offers the "Download installer" escape hatch (opens Setup.exe URL in browser); generic Failed just offers Retry.
- New red banner in `LaunchControlsPanel.axaml` with context-sensitive text: "Update to vX.Y.Z didn't apply. Reboot and try again, or download the installer."

### Tests

8 new tests in `VelopackLogApplyFailureDetectorTests` using real temp files so tail-reading is exercised end-to-end. Cover: no log file, recent error, old error, no apply errors, multiple errors (picks most recent), midnight rollover, IO exception safety, large log (tail-read correctness). Total: 263 tests (was 255).

### Adoption chicken-and-egg

Users currently stuck on v1.10.2/v1.10.3 because their apply keeps failing won't receive v1.10.4 for the same reason. They need to reboot (clears file locks) and retry, or download Setup.exe manually. Once v1.10.4 lands successfully, any future apply failure surfaces the banner immediately. Documented in `docs/dev/LEARNINGS.md`.

## v2.0 - ComeOnOver Integration

- [ ] Native ComeOnOver desktop app detection and launch (when available)
- [ ] Link to ComeOnOver download page if not installed
- [ ] ComeOnOver version display

## v3.0 - Cross-Platform

- [ ] macOS support (Claude Desktop path resolver)
- [ ] Linux support (Claude Desktop AppImage/deb path resolver)
- [ ] Platform-specific path resolver implementations behind `IClaudePathResolver`
- [ ] CI/CD pipeline for multi-platform builds

## Monetisation

- [ ] In-app advertising (tasteful, non-intrusive - planned for a future version)
- [ ] GitHub Sponsors / Ko-fi as an alternative for users who prefer ad-free
- [ ] Ads will never appear in v1.x - planned for a later major version once the user base is established

## Backlog / Under Consideration

- [ ] **Show-from-tray button** (other half of the v1.10.5 Hide feature) - enumerate hidden top-level windows by PID, `ShowWindow(SW_SHOW) + SetForegroundWindow`. Appears on TrayCard rows so hidden slots can be restored without digging through Claude's system-tray menu. Risk: Claude's main-window `Hwnd` can change across hide/show cycles, the scanner may need to re-discover it. Test before shipping.

- [ ] **Investigate Velopack retry-count configuration** - the 10x1s backup-retry window that caused the v1.10.2 -> v1.10.3 failures is hardcoded in Velopack 0.0.1298. Check if `vpk pack` exposes a flag to raise it, or whether newer Velopack versions default to a longer window. Even 30x2s would cover most Defender scan durations. Low-priority because the v1.10.4 banner now gives users a clean escape hatch.

- [ ] **Claude usage tracker + cooldown timer** - surface the data from [claude.ai/settings/usage](https://claude.ai/settings/usage) directly in the launcher so users don't have to alt-tab into the web UI to see their current session %, weekly limits, resets, or extra-usage spend. Also an independently-useful cooldown timer per slot showing how long until the 5-hour rolling window resets. Three implementation paths with tradeoffs:
    - Embedded WebView (~100 MB install bloat, auth complexity, but simplest to build)
    - Reverse-engineered internal API (fast/small but fragile, Anthropic can break it)
    - Local cooldown timer only (zero network dependency, delivers the most-requested piece, pragmatic MVP)
    Recommendation: start with local-only cooldown timer as MVP, layer on remote usage stats later if a stable API appears. Opt-in via `AppSettings.ShowUsageTracker` default OFF.

- [ ] **Shared extension store across slots** - each `ClaudeSlot{N}\Claude Extensions\` is currently a separate copy of the extension tree. 1.22 GB of duplication measured across ClaudeSlot1-5 on Lewis's machine. Junction points verified working locally. Full design draft with 5 open design questions in [`docs/dev/EXTENSION-STORE.md`](docs/dev/EXTENSION-STORE.md). First step: manually junction one small extension (Filesystem, 12 MB) from slot 2 -> slot 1's copy, verify Claude still loads/uninstalls it cleanly before writing the service layer.

- [ ] **Per-slot activity preview** - surface what each slot is doing at a glance. Open design questions in the existing backlog thinking: thumbnail approach (visual, privacy-mixed), metadata approach (textual, bigger privacy concern), activity-signal approach (CPU %, last-interacted timestamp, minimal privacy exposure). Likely combo: activity signal always visible + optional thumbnail behind a settings toggle OFF by default. Thumbnails stored in-memory only.

- [ ] Submit to awesome-avalonia list
- [ ] Reddit / HN launch post
