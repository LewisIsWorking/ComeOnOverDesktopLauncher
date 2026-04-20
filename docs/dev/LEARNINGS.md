# CoODL — Claude's learnings

Repeated mistakes and their fixes. Written so future-me (any Claude session) does not re-discover these by touching the stove.

Read this FIRST at the start of any CoODL session. Add to it whenever a lesson is learned twice. If a topic accumulates and pushes this file near the 200-line limit, extract it into a sibling `docs/dev/<TOPIC>.md` and cross-reference from here.

## Companion docs

- **`docs/dev/BUILD-AND-TOOLING.md`** — line-count audit, PowerShell / heredoc quirks, commit-message workflow, CI runner gotchas, screenshot workflow, UI-Automation snippets, the release checklist.
- **`docs/dev/VELOPACK.md`** (v1.10.0+) — Velopack packaging + auto-update CI pipeline: local `vpk pack` commands, the `PublishSingleFile=false` and `fetch-depth: 0` invariants, the `--token` gotcha, delta-package generation via `vpk download github`, post-ship validation strategy, code-signing future plans.
- **`docs/dev/REFACTOR-AND-XAML.md`** — NSubstitute / fixture gotchas, Avalonia compiled-binding pitfalls (`string` -> `Color`, `Tapped` vs `PointerPressed`, `IsHitTestVisible` on child images/overlays), WMI scanner Electron quirk, close-to-tray process state, clipboard bitmap capture.
- **`docs/MIGRATION.md`** — user-facing v1.9.x -> v1.10.0 migration guide (portable .exe -> Setup.exe + auto-update).

## Hard rules (repeated here for emphasis)

These come from the project owner and are non-negotiable:

1. **Every file ≤ 200 lines** (.cs, .axaml, .md, .ts, etc.). Only exception is `package.json`. When a file grows past 200, extract via OOP — split, don't trim. Never remove comments, code or whitespace to hit the limit.
2. **100% test pass rate** before any push. Build must be 0 warnings, 0 errors.
3. **Never suppress warnings** with `NoWarn`, `#pragma`, or `[SuppressMessage]`. Fix the root cause.
4. **Never strip logging.** Diagnostic/info/warning/error logging stays permanently once added. Logging is never "temporary".
5. **Always `dotnet run` + visually verify** before committing a visual change. Fresh screenshot → `docs/screenshots/` → ROADMAP updated referencing it.
6. **Plan before implementing.** Present options, wait for the user to pick, then build. No unilateral "I'll just build it" moves.
7. **SOLID / MVVM / event-driven** throughout. ViewModels never touch system APIs directly — everything goes through an `I<Service>` abstraction.
8. **Read `docs/` first.** At the start of any CoODL session, read this file and the two companion docs before touching code. Saves re-discovering things.

## CoODL architecture invariants

- `MainWindowViewModel` depends only on `I<Service>` abstractions. No direct WMI / filesystem / registry calls.
- Core layer (`ComeOnOverDesktopLauncher.Core`) has zero Avalonia references. Keep UI out of Core. The only exception is that Core services can return raw bytes (e.g. `byte[]?` for thumbnails) and the UI layer materialises them into Avalonia types.
- Every service has an `I<Service>` interface in `Services/Interfaces/` and a concrete implementation in `Services/`.
- List VMs own reconciliation — they mutate their `ObservableCollection` in place, never reassign. Preserves Avalonia binding identity so row state (edit-in-progress text) survives refreshes.
- Reconciliation is identity-preserving: match by stable key (slot number for slots, PID for externals), update in place, add missing, remove gone. Never clear-and-repopulate.
- Pure functions / testable logic separated from IO-orchestration services. Example: `SeedCacheValidators` (pure validation) extracted from `FileSlotSeedCache` (IO orchestration).
- Scanner→Classifier→ViewModel pipeline. Scanner returns raw `ClaudeProcessInfo`. Classifier returns typed `SlotProcessInfo` / `ExternalProcessInfo` / null. VMs filter + reconcile.
- `IThumbnailableViewModel` is the shared surface across both `ClaudeInstanceViewModel` and `ExternalInstanceViewModel`, letting `ThumbnailRefresher` treat both uniformly without a common base class (v1.9.1+).
- Per-row callbacks live as `Action<T>? OnSomething` properties on the list VM, wired via `SlotCallbackBinder.Bind` (slots) + `SlotCallbackBinder.BindExternal` (externals). Keeps row VMs free of service dependencies.
- **Velopack auto-update** (v1.10.0+): `VelopackApp.Build().Run()` MUST run as the first line of `Program.Main`, before Avalonia boots. Velopack's `UpdateManager` is a concrete type wrapped by our `IAutoUpdateService` adapter for testability. Update orchestration lives in `MainWindowUpdateViewModel` (sub-VM pattern) + `UpdateOrchestrator` (state machine) so `MainWindowViewModel` stays under the 200-line cap. Full design rationale in `docs/dev/VELOPACK.md`.
- **Self-heal at startup** (v1.10.2+): where an external system (Velopack, Claude Desktop, the Windows Shell) has a known bug that leaves our app in a broken state, we prefer shipping a local heal-on-startup check over waiting for an upstream fix. The pattern: interface + two injectable probes (production resolver + a mock for testing), dev-build guard so `dotnet run` never attempts the heal, defensive `try/catch` at the top of the heal method so a broken probe can't block startup, and an enum return type so callers (and tests) can assert which branch ran. `IShortcutHealer` is the reference implementation.

## Diagnostic honesty — lessons from the 2026-04-20 update-failure investigation

These come from a session where I called a reproducible bug "transient", mistook a dev build for the installed version, and churned on half-fixes. Future-me, don't do these:

- **"Transient" is a conclusion, not an opening hypothesis.** When an update, install, or external system fails, the first instinct shouldn't be "maybe it's flaky, retry" — it should be "what evidence tells me *why* it failed?". Cheap diagnostics FIRST: read the relevant log (Velopack writes to `%LOCALAPPDATA%\<AppName>\velopack.log`, CoODL writes to `%APPDATA%\<AppName>\logs\`), run `handle64.exe` from Sysinternals (downloadable from `https://live.sysinternals.com/handle64.exe`) against the install dir to find file locks, check process trees with `Get-WmiObject Win32_Process`. A retry without evidence wastes the user's time and erodes their trust. Two consecutive failures with identical symptoms = reproducible bug, not flake.

- **Dev builds can impersonate the installed version.** A `dotnet run` in `bin\Debug\net10.0\` produces an .exe that shows the same version footer as the installed Velopack build (because both read the same csproj `<Version>`). The ONLY reliable way to distinguish them is the binary path:
  - Installed: `%LOCALAPPDATA%\ComeOnOverDesktopLauncher\current\ComeOnOverDesktopLauncher.exe`
  - Dev build: `...\RiderProjects\ComeOnOverDesktopLauncher\ComeOnOverDesktopLauncher\bin\Debug\net10.0\ComeOnOverDesktopLauncher.exe`
  Before claiming "update succeeded, user is now on vX.Y.Z", ALWAYS check `Get-Process ComeOnOverDesktopLauncher | Select-Object Path` AND `[System.IO.File]::GetLastWriteTime` on the installed exe. If the install-dir exe's mtime pre-dates the release commit, the update did NOT apply regardless of what the UI footer says. I made this mistake twice in one session. Kill stray dev builds before diagnostic work.

- **Velopack silently relaunches the old version on apply failure.** The `UpdateManager.ApplyUpdatesAndRestart()` call is documented as "never returns" because it exits the process. What's NOT documented: when apply fails (e.g. backup-and-swap hits "file in use"), Velopack *catches the error internally*, logs `[ERROR] Apply error:` to `velopack.log`, and relaunches the ORIGINAL exe. The launcher code sees a successful restart into the old version — no exception is thrown on the C# side. **The only signal available is the Velopack log**. Detection pattern for v1.10.4+: read the tail of `%LOCALAPPDATA%\<AppName>\velopack.log` on startup, look for `[ERROR] Apply error:` with timestamp within the last ~2 minutes. See the `IUpdateApplyFailureDetector` design in the v1.10.4 roadmap entry.

- **Velopack's 10×1s retry window on backup is often too short.** The apply step extracts 237 files to `packages\VelopackTemp\` then tries to move `current\` to a backup. Windows Defender's real-time scan can hold read handles on the fresh files for 10-15 seconds after write, causing the move to fail. Observed on Lewis's machine, three identical failures in a row, same minute. `handle64.exe` found no holders when the launcher was dead, confirming the lock is grabbed DURING Velopack's apply, not before. Also affects `Setup.exe --silent` which uses the same apply codepath. Full context in the v1.10.4 roadmap entry.

- **"Continue" isn't always the right response to a stuck investigation.** When a fix attempt fails, stop and write a plan BEFORE another attempt. I burned several turns trying slightly-different fixes when I should have stopped after the second failure to write the proper diagnostic + design doc. The user had to explicitly tell me to stop. Signs to stop: repeated failures with the same symptom; speculating about causes rather than measuring; running out of tokens while still flailing. Better to ship a honest roadmap entry + one well-designed follow-up than three half-implementations.

## Memory / mental model

- The user runs the CoODL launcher on Windows (Dell laptop is Linux but not the target).
- ClaudeSlot{N} data directories live at `%LOCALAPPDATA%\ClaudeSlot{N}\`.
- Seed cache is at `%APPDATA%\ComeOnOverDesktopLauncher\seed\`.
- Logs are at `%APPDATA%\ComeOnOverDesktopLauncher\logs\launcher-YYYY-MM-DD.log`.
- The user is on mobile often but this specific project is exclusively Windows-local (not GitHub API).
- User's Windows GitHub Desktop is the shiftkey fork v3.4.12-linux1.
- AutoHotkey v2 script at `C:\Users\Lewis\Documents\AutoHotkey\Continue.ahk`.
- The user has a multi-monitor setup. The primary monitor is 2560x1440. Automated clicks via `mouse_event` + `SetCursorPos` can mis-target on high-DPI primary monitors; prefer `Windows-MCP:Click` tool which handles DPI correctly, or use the secondary monitor for live-verify runs.
