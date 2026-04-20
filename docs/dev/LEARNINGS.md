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

## Memory / mental model

- The user runs the CoODL launcher on Windows (Dell laptop is Linux but not the target).
- ClaudeSlot{N} data directories live at `%LOCALAPPDATA%\ClaudeSlot{N}\`.
- Seed cache is at `%APPDATA%\ComeOnOverDesktopLauncher\seed\`.
- Logs are at `%APPDATA%\ComeOnOverDesktopLauncher\logs\launcher-YYYY-MM-DD.log`.
- The user is on mobile often but this specific project is exclusively Windows-local (not GitHub API).
- User's Windows GitHub Desktop is the shiftkey fork v3.4.12-linux1.
- AutoHotkey v2 script at `C:\Users\Lewis\Documents\AutoHotkey\Continue.ahk`.
- The user has a multi-monitor setup. The primary monitor is 2560x1440. Automated clicks via `mouse_event` + `SetCursorPos` can mis-target on high-DPI primary monitors; prefer `Windows-MCP:Click` tool which handles DPI correctly, or use the secondary monitor for live-verify runs.
