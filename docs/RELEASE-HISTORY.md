# CoODL - Release history

Archived release notes from v1.0 through v1.8.3. Extracted from `ROADMAP.md` during v1.9.2 to keep that file under the 200-line limit. The current `ROADMAP.md` covers only the v1.9.x series onwards.

## v1.0 - Released
- [x] Launch one or more Claude Desktop instances simultaneously
- [x] Fixed named slots to preserve login sessions between launches
- [x] Open ComeOnOver web app (https://comeonover.netlify.app) from launcher
- [x] Settings persist between sessions (slot count)
- [x] Claude install auto-detection (MSIX/WindowsApps + PowerShell fallback)
- [x] Resizable window with sensible minimum size
- [x] Auto-update path cache on every launch (handles Claude updates silently)
- [x] GitHub Actions release pipeline - self-contained .exe and .zip, no .NET required

## v1.1 - Released
- [x] System tray icon - minimize to tray, right-click quick-launch menu
- [x] Close-to-tray - clicking X hides the window, use tray Quit to fully exit
- [x] Running instance count with refresh button
- [x] Fix: windowed process count (no longer inflated by Electron child processes)

## v1.2 - Released

![v1.2 UI](docs/screenshots/photo_2026-04-15_16-13-35.jpg)

- [x] Per-instance resource display: CPU %, RAM usage, process uptime
- [x] Combined totals: total CPU and RAM across all running Claude instances
- [x] Resource data auto-refreshes every 5 seconds
- [x] Manual refresh button
- [x] Slot naming - users can name each instance (e.g. "Work", "Personal")
- [x] Slot names persist to settings and survive app restarts
- [x] Fix: clicking outside slot name field correctly deselects it

## v1.3 - Released
- [x] Login persistence - slots seeded with cookies from default Claude profile on first use
- [x] Version number displayed in UI, auto-updates from csproj version property

## v1.4 - Released
- [x] Launch on Windows startup toggle in UI
- [x] App starts minimised to tray when launched on startup (--minimised flag)
- [x] Login status indicator per slot: filled circle (logged in) or empty circle (not yet seeded)
- [x] Better error messaging when Claude is not installed - includes download link hint
- [x] Registry abstraction (IRegistryService) for future cross-platform support
- [x] Zero build warnings - CA1416 platform annotations added throughout
- [x] .gitattributes added to normalise line endings

## v1.5 - Released
- [x] Configurable resource monitor refresh interval (1-60 seconds)
- [x] Notify user when a new launcher version is available on GitHub

## v1.6 - Released

![v1.6 UI](docs/screenshots/photo_2026-04-15_16-13-35.jpg)

### Diagnostic logging
- [x] File logging via `ILoggingService` / `FileLoggingService`
- [x] Rolling daily log files at `%APPDATA%\ComeOnOverDesktopLauncher\logs\launcher-yyyy-MM-dd.log`
- [x] Every stage of the launch flow is logged (path resolution, slot seeding, process start)
- [x] Thread-safe logging with I/O failures swallowed - logging can never crash the app
- [x] "Logs" button in resource-totals row opens the log folder in Explorer
- [x] `[CallerMemberName]` auto-tags every log line with the method that emitted it

### Launch semantics
- [x] `Launch Claude` button now opens the requested number of **additional** instances
- [x] New `ISlotManager.GetNextFreeSlots(count)` scans for the next unoccupied slot numbers
- [x] Slot occupation detected via commandline inspection (`--user-data-dir=...\ClaudeSlotN`) not process count
- [x] Default Claude profile no longer interferes with slot detection
- [x] Safety cap at slot 100 so malformed state can never hang the UI
- [x] Input label renamed from "Claude instances to open" to "Additional instances to open" for clarity

### Login persistence improvements
- [x] Cookies seeding uses `FileShare.ReadWrite` so it works while Claude is running
- [x] Copied cookies are verified against the SQLite 3 magic header; invalid copies are discarded
- [x] Slots opened while other Claude instances are running now inherit login correctly

### UI polish
- [x] Per-instance row layout now aligns with the combined-totals border above
- [x] CPU / RAM / Up columns are vertically under Total RAM / Total CPU columns
- [x] Close button column aligns with the "Logs" button column

## v1.7.1 - Released

![v1.7.1 UI](docs/screenshots/photo_2026-04-18_v1.7.1.png)

- [x] **v1.7.1 fix** - always re-seed from cache on launch. v1.7.0's `IsSeeded` only checked Cookies size, so a slot with leftover Cookies from one session and a stale `Local State` from another would skip the cache, and Chromium could not decrypt with the mismatched key -> surprise login wall. Re-seeding is idempotent and cheap (~15 ms for three small files); `TrySeed` fails silently if the cache is empty or a destination is locked, so calling it on every launch is safe and corrects any drifted slot state. Also fixed four timing-flaky `SlotProcessMonitorTests` that broke v1.7.0's CI build on slow GitHub Actions runners (replaced fixed `Thread.Sleep` with polling-with-deadline).
- [x] **Start maximised** - launcher now opens maximised by default (`WindowState="Maximized"` on the main window) so running/logged-in instances and their resource stats are visible without manual resize.
- [x] **Seed cache** - persistent `%APPDATA%\ComeOnOverDesktopLauncher\seed\` snapshot of a known-good Claude login state (Cookies + Local State + Preferences)
- [x] New `ISlotSeedCache` / `FileSlotSeedCache` services capture and apply the snapshot atomically; failed captures leave the previous cache intact
- [x] SQLite magic-header + JSON `os_crypt.encrypted_key` validation rejects corrupt or half-written cache files
- [x] `SlotInitialiser` now seeds from the cache *first*, falling back to default-profile / other-slot Cookies only when the cache has never been populated
- [x] New slots opened while the default Claude profile is running now come up logged in (previously required closing Claude first)
- [x] New `ISlotProcessMonitor` / `SlotProcessMonitor` polls running Claude slots and raises `SlotClosed` events
- [x] Pure-function `SlotProcessTickRunner` extracted so transition logic is unit-testable without a real timer
- [x] `SlotSeedCacheUpdater` subscribes to `SlotClosed`, waits 5s for Electron helpers to release locks, then opportunistically refreshes the seed cache
- [x] 162 tests passing (up from 119), zero warnings, zero errors

## v1.8.0 - Released

![v1.8.0 UI](docs/screenshots/photo_2026-04-18_v1.8.0.png)

- [x] **Show Claude Desktop version in UI** - read `FileVersionInfo.ProductVersion` from the resolved claude.exe and display it in the footer alongside the launcher version (e.g. `Launcher v1.7.1 - Claude 1.3109.0.0`). New `IClaudeVersionResolver` service + property on `MainWindowViewModel` + text in the footer region of `MainWindow.axaml`.
- [x] **Split UI for launcher-managed vs externally-launched Claude instances** - windowed claude.exe processes are now cleanly split into two lists: the slot list (top) for processes with `--user-data-dir=...\ClaudeSlotN`, and the External Claude instances section (bottom) for everything else (default-profile Claude launched from the Start menu, or Claude launched by some other tool). Each windowed Claude appears in exactly one list, never both.
    - Required a non-obvious WMI enrichment fix: Chromium/Electron's "browser main" process (the one with the visible window) reports an empty args list to WMI - its `--user-data-dir` flag is only copied into child processes during fork. `WmiClaudeProcessScanner` now queries `ParentProcessId` too, walks each windowed main's direct children, and when the main's own cmdline is missing the flag, extracts `--user-data-dir=...` from any child and appends it. Without this, every windowed Claude mis-classified as external.
    - Windowed-only filter via `Process.MainWindowHandle != IntPtr.Zero` suppresses the ~10 Electron child processes (renderer, GPU, crashpad, audio/video/network utility services) that would otherwise flood the UI.
    - Slot list now relabels `InstanceNumber` with the real slot number from the cmdline, not the sequential enumeration index. Slot 3 renders as "Instance 3" even when slots 1 and 2 are closed (the previous enumeration approach mis-labelled this case).
    - New `SlotInstanceListViewModel` (parallel to `ExternalInstanceListViewModel`) owns the filter + reconcile pipeline. Reconciliation is identity-preserving (same VM instance per slot number across refreshes), so row-level state like edit-in-progress name text survives.
    - Close button on external rows pops a custom destructive-severity `ConfirmDialog` (new `IConfirmDialogService` + `Views/ConfirmDialog.axaml`) with PID, uptime and full command line before calling `IProcessService.KillProcess`. User cancel is the default - Esc and the window close button both cancel.
- [x] **Launch sequencing owned by `ClaudeInstanceLauncher`** - new `LaunchInstances(int count)` method owns the full slot-pick + seed + launch sequence. `MainWindowViewModel` no longer depends on `ISlotManager` or `ISlotInitialiser`; it just calls `_launcher.LaunchInstances(SlotCount)`.
- [x] **`Copy window screenshot to clipboard` button** - shipped in v1.7.3 using Avalonia 12's `RenderTargetBitmap.Render(visual)` + `ClipboardExtensions.SetBitmapAsync` (not GDI) - rendering the visual tree directly gives reliable results regardless of window state (maximised, partially covered, off-screen) that the original GDI `CopyFromScreen` approach would have struggled with. Image lands on the clipboard in every relevant Windows format simultaneously (`image/png`, `PNG`, `DeviceIndependentBitmap`, `Format17`, `Bitmap`) so it pastes into Slack/Discord/Word/Paint without fuss.
- [x] 229 tests passing (up from 162 in v1.7.1), zero warnings, zero errors

## v1.9.2 - Released

![v1.9.2 UI](docs/screenshots/photo_2026-04-19_v1.9.2.png)

Third and final release of the card/thumbnail series. v1.9.2 lands click-to-enlarge + two bug fixes that surfaced during v1.9.1's live-verify: the green "Logged in" pill was invisible inside the new cards, and the preview popup I first wired up was upscaling a 240x150 cached thumbnail which looked blurry.

### Login pill fix

- [x] **Brush properties promoted from `string` to `IBrush`** on `ClaudeInstanceViewModel`. The old code stored `LoginStatusBackground` as a hex string like `#2E7D32` and bound it via `<SolidColorBrush Color="{Binding LoginStatusBackground}"/>`. That worked in the v1.8.x row templates but stopped coercing correctly inside the v1.9.1 UserControl DataTemplates under Avalonia 12's compiled bindings - the pill rendered transparent. Now `LoginStatusBackground` returns an `IBrush` directly and the XAML binds `Border.Background="{Binding LoginStatusBackground}"` with no coercion step in between.
- [x] **SlotCard.axaml simplified** - the nested `<Border.Background><SolidColorBrush/></Border.Background>` dance is gone; direct `Background="{Binding}"` does the right thing now that the VM returns the right type.
- [x] **Visually verified** in the v1.9.2 screenshot above - both slots show their green "Logged in" pills next to the Slot N pills.

### Click-to-enlarge preview

- [x] **New `IThumbnailPreviewService.Show(int processId, Bitmap? fallback, string title)`** - opens a lightbox-style Avalonia window showing the slot/external thumbnail at 900x600 centred on the launcher. Click anywhere or press Esc to close.
- [x] **Fresh high-resolution capture at preview time** - the service takes the `processId` and calls `IWindowThumbnailService.CapturePngBytes(pid, 1920, 1200)` at the moment of preview, then decodes the fresh bytes into a `Bitmap` and renders that. The old approach of reusing the cached 240x150 card thumbnail looked awful when upscaled; fresh capture gives a crisp near-native view of whatever the Claude window currently shows.
- [x] **Cached thumbnail as fallback** - if fresh capture returns null (tray-resident window, PID exited, GDI pressure) the service falls back to the caller-supplied cached bitmap so tray slots still produce a useful preview of their last-known frame. Both-null is a silent no-op so callers never need to guard.
- [x] **`ThumbnailPreviewWindow.axaml`** - lightbox UI: 900x600, centred on owner, transparent bg, full-bleed Image with 16px margin. A small "Click anywhere or press Esc to close" hint sits in the top-right at `IsHitTestVisible="False"` so it doesn't block the click-to-close behaviour. Bitmap ownership stays with the source VM; the window holds a reference for its lifetime but never disposes it.

### Command plumbing

- [x] **`ShowPreviewCommand` on both row VMs** - `ClaudeInstanceViewModel` and `ExternalInstanceViewModel` each have a `[RelayCommand] ShowPreview()` method that invokes a callback supplied by the list VM. The list VMs (`SlotInstanceListViewModel`, `ExternalInstanceListViewModel`) each own an `OnShowPreview` property wired in `SlotCallbackBinder`.
- [x] **`SlotCallbackBinder.BindExternal`** - new companion method for the external list VM so the main window doesn't need to inline the callback lambda.
- [x] **DI registered** for `IThumbnailPreviewService` alongside the other service registrations in `App.axaml.cs`.

### Event-wiring bug fix: `Tapped` -> `PointerPressed`

- [x] **`Tapped` on `Border` inside a DataTemplate didn't fire reliably** under Avalonia 12 - clicks registered but the XAML `Tapped="OnThumbnailTapped"` attribute-based handler never ran. v1.9.2 switches to `PointerPressed` which is lower-level and fires unconditionally on pointer-down. The handler filters to `LeftButtonPressed` so right-click stays available for future context menu work.
- [x] **`IsHitTestVisible="False"` on card `Image` children** - belt-and-braces so the Image child (which fills the thumbnail Border) doesn't swallow pointer events before they reach the Border handler.

### Diagnostic logging (permanent)

- [x] **Every preview request emits an info-level log line** - `[Show] Preview requested: title='...' pid=N fresh=ok|null fallback=ok|null` followed by `[OpenWindow] Preview window shown (owner=main) for '...'`. Useful for diagnosing preview issues in the wild. Per the hard rule, this logging stays permanently - it's never "temporary".

### Numbers

- 247 tests passing, unchanged from v1.9.1 - no new behaviour needed test coverage (the new service is trivially tested by proxy through the preview callbacks, and the pill-brush change is a pure shape-of-data refactor that existing VM-construction tests cover).
- 0 warnings, 0 errors.
- All files <= 200 lines.

## v1.9.1 - Released

![v1.9.1 UI](docs/screenshots/photo_2026-04-19_v1.9.1.png)

Second of three incremental releases in the card/thumbnail series. v1.9.1 completes the visual migration: the row-based instance lists are replaced with a WrapPanel grid of full-size cards, and external Claude instances join the thumbnail pipeline alongside launcher-managed slots. v1.9.2 will add the click-to-enlarge preview.

### Grid card layout

- [x] **Three new UserControls** - `SlotCard.axaml`, `TrayCard.axaml`, `ExternalCard.axaml` - each rendering one row's worth of state in a 256px-wide card with a 240x150 thumbnail at the top, pills + metadata in the middle, and the CPU/RAM/uptime/action row at the bottom. Each card has a single clear responsibility so future evolution (drag-to-reorder, per-card context menus, click-to-enlarge) has a clean home.
- [x] **WrapPanel grid** - all three list controls (`SlotInstanceList`, `TrayInstanceList`, `ExternalInstanceList`) now use an `ItemsPanelTemplate` with a horizontal `WrapPanel` containing the appropriate card host. On default window widths the grid tiles cleanly to 2 columns; narrow windows collapse to a single column automatically without any explicit breakpoint logic.
- [x] **TrayCard "Hidden" overlay** - tray-resident slots show their last-captured thumbnail at 0.6 opacity with a "Hidden" badge in the top-right corner, communicating stale / frozen state at a glance. Read-only italic nickname (you cannot usefully edit a name for a slot whose window is gone) and a "Quit" button (force-kill, no confirm dialog - there is no visible window whose unsaved state to protect).
- [x] **ExternalCard de-emphasised style** - grayer border (#2A2A2A), darker background (#151515), thumbnail at 0.85 opacity, muted foreground. Visually says "I didn't launch this, you did" without being visually dominant.

### Externals join the thumbnail pipeline

- [x] **New `IThumbnailableViewModel` interface** - shared surface (`ProcessId`, `Thumbnail`, `UpdateThumbnailFromBytes`, `ClearThumbnail`) implemented by both `ClaudeInstanceViewModel` and `ExternalInstanceViewModel`. Lets `ThumbnailRefresher` treat both row types uniformly without imposing a common base class.
- [x] **`ExternalInstanceViewModel` gains thumbnail support** - same observable `Thumbnail` property, same no-op-on-null contract for `UpdateThumbnailFromBytes` (frozen-thumbnail behaviour applies to externals that go to tray too), same `ClearThumbnail` for explicit blank on toggle-off. Adds a `ProcessId => Pid` alias so the interface name matches without renaming the existing XAML-bound `Pid` property.
- [x] **`MainWindowViewModel.RefreshResources` captures externals** - the `ThumbnailsEnabled` gate now loops over both `SlotInstances.Items` and `ExternalInstances.Items` in the poll tick. `OnThumbnailsEnabledChanged` extends the toggle-off clear to cover externals too.

### Small-but-nice details

- [x] **Thumbnails bound to `IImage`** via the `Bitmap?` property - same pattern as v1.9.0, unchanged. The UI-layer materialisation (bytes -> Bitmap) stays in the VM so Core still has zero Avalonia references.
- [x] **TrayCard still shares `ClaudeInstanceViewModel`** with `SlotCard` - no duplicated VM state, just a different view template. The "Hidden" overlay is XAML-only because the underlying data (a tray-resident slot) doesn't need new properties to tell the story.
- [x] **ExternalCard's close button still goes through the confirm dialog** owned by `ExternalInstanceListViewModel` - the `CloseCommand` binding and its dialog flow survived the card migration unchanged.

### Numbers

- 247 tests passing, unchanged from v1.9.0 - no new behaviour needed test coverage (the VM lifecycle is the same; only the view template moved). `ThumbnailRefresher` was verified by proxy through existing `MainWindowViewModel` tests that still pass.
- 0 warnings, 0 errors.
- All files stay <= 200 lines; `ExternalInstanceViewModel` grew from 153 to 184 lines with the thumbnail additions, still comfortably under.

