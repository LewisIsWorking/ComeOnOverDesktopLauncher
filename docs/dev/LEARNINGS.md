# CoODL — Claude's learnings

Repeated mistakes and their fixes. Written so future-me (any Claude session) does not re-discover these by touching the stove.

Read this FIRST at the start of any CoODL session. Add to it whenever a lesson is learned twice.

## Hard rules (repeated here for emphasis)

These come from the project owner and are non-negotiable:

1. **Every file ≤ 200 lines** (.cs, .axaml, .md, .ts, etc.). Only exception is `package.json`. When a file grows past 200, extract via OOP — split, don't trim. Never remove comments, code or whitespace to hit the limit.
2. **100% test pass rate** before any push. Build must be 0 warnings, 0 errors.
3. **Never suppress warnings** with `NoWarn`, `#pragma`, or `[SuppressMessage]`. Fix the root cause.
4. **Never strip logging.** Diagnostic/info/warning/error logging stays permanently once added. Logging is never "temporary".
5. **Always `dotnet run` + visually verify** before committing a visual change. Fresh screenshot → `docs/screenshots/` → ROADMAP updated referencing it.
6. **Plan before implementing.** Present options, wait for the user to pick, then build. No unilateral "I'll just build it" moves.
7. **SOLID / MVVM / event-driven** throughout. ViewModels never touch system APIs directly — everything goes through an `I<Service>` abstraction.

## Build / tooling gotchas

### Line-count audit command

Run this any time you suspect drift. It's also what the CI guard checks:

```powershell
$root = "C:\Users\Lewis\RiderProjects\ComeOnOverDesktopLauncher"
Get-ChildItem -Path $root -Recurse -File -Include *.cs, *.axaml, *.md |
    Where-Object { $_.FullName -notmatch "\\(bin|obj|TestResults|\.git|node_modules)\\" } |
    ForEach-Object {
        $lines = [System.IO.File]::ReadAllLines($_.FullName).Count
        [PSCustomObject]@{ Lines = $lines; File = $_.FullName.Replace("$root\", "") }
    } |
    Where-Object { $_.Lines -gt 200 } |
    Sort-Object -Property Lines -Descending |
    Format-Table -AutoSize
```

Do NOT use `Measure-Object -Line` — it undercounts on files with the final newline missing. Always use `[System.IO.File]::ReadAllLines($path).Count`.

### Kill the running launcher before rebuilding

```powershell
Get-Process -Name "ComeOnOverDesktopLauncher" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
```

If the launcher is running, `dotnet build` fails with `MSB3027: The file is locked by: "ComeOnOverDesktopLauncher (PID)"` because msbuild can't overwrite the DLL. Always kill before building after a live-run session.

### PowerShell / heredoc quirks in this codebase

- `$PID` is a read-only PowerShell automatic variable. Use `$procId` instead.
- Files use LF line endings (enforced by `.gitattributes`). When doing replacements with Set-Content, use `$LF = "`n"` rather than `"`r`n"`.
- Single-quoted heredoc `@'...'@` preserves C# `$"..."` interpolation. Double-quoted `@"..."@` eats the `$`.
- For multi-line git commit messages: **always** write to `$env:TEMP\*.txt` then `git commit -F <tempfile>`, never pipe a heredoc directly to `git commit -m`. Piping breaks on PowerShell line-continuation rules. Delete the temp file after.

### Working directory drift

Some `start_process` / `bash` calls spawn a subshell with `bin/Debug/` as the cwd (because the last process the current shell ran was in that directory). Always start commands with an explicit `Set-Location` or `cd` to the repo root. Symptom is `ReadAllLines` with paths like `bin\Debug\net10.0\ComeOnOverDesktopLauncher.Core\...`.

### CI runner and ImageMagick

The GitHub Actions release workflow does NOT have ImageMagick installed. Always commit the pre-rasterised binaries (`.ico`, `.png`) alongside the SVG source. The SVG + `build-icons.ps1` live at `docs/design/` as the regeneration path.

### .NET 10 on the CI runner

The release workflow uses a `regex → net9.0` substitution in `.github/workflows/release.yml` because GitHub Actions doesn't have .NET 10 preinstalled yet. Don't remove that step.

## Refactor gotchas

### `using NSubstitute;` does NOT flow through test helpers

When splitting test files, the `Returns(...)` extension method lives in `NSubstitute`, not in xUnit or any base namespace. **Every new test file needs `using NSubstitute;`** if it calls `.Returns(...)`, `.Received()`, `.When(...)` etc. Cost of forgetting: a build iteration per file. Copy the full `using` block verbatim when extracting test classes.

### Static helper methods on fixtures

When a test helper calls a static factory method on a fixture class (e.g. `MyFixture.Snap(pid)`), the test file needs to qualify it with the full class name unless imported. Helpers live on the fixture class, not as free statics, so they're always called `FixtureName.Helper(args)`. It reads ugly but beats duplicating across 15 test methods.

### Avalonia compiled-binding XAML split

Splitting a large `Window.axaml` into `UserControl`s is safe ONLY if:

- Each `UserControl.axaml` has `x:DataType="vm:MainWindowViewModel"` (or whatever the parent's DataType is) so inherited bindings still compile.
- Each `UserControl.axaml.cs` has `partial class` + `InitializeComponent()` call.
- Every binding that used to reference the code-behind (e.g. `Click="OnCopyScreenshotClick"`) needs to be replaced with a `RoutedEvent` pattern: name the button with `x:Name="MyButton"`, expose an event on the UserControl, let MainWindow subscribe.
- Use `x:Name`, not `Name=` — Avalonia's compiled-XAML source generator requires the `x:` prefix.

### WMI scanner — Electron browser-main quirk

Chromium/Electron's main process (the one with the visible window) reports an **empty args list** to WMI. Its `--user-data-dir` flag only appears on child processes. If you filter by windowed + cmdline match, every Claude window mis-classifies as external.

Fix in `WmiClaudeProcessScanner`: query `ParentProcessId` too, walk each windowed main's direct children, and when the main's own cmdline lacks the flag, extract `--user-data-dir=` from any child and synthesise a minimal enriched cmdline. Uses `ExtractFlagValue` helper that handles both quoted and bare values.

### Close-to-tray process state

A Claude slot that's been close-to-tray'd has:

- Main process still alive (PID 76748 or whatever)
- `MainWindowHandle == 0` (window is hidden)
- ~12 child processes still alive under it

The windowed-only filter (`MainWindowHandle != 0`) correctly suppresses Electron children but also incorrectly suppresses tray-resident slot mains. Use parent-process identity instead: a claude.exe is a "main" if its parent is not also a claude.exe.

### Avalonia `Grid` SizeToContent constraint

Don't use `RowDefinitions="Auto,*,Auto"` inside a window with `SizeToContent`. Circular constraint — the `*` wants the whole space, but there's no fixed space. Use `Auto,Auto,Auto` and let content size itself.

### Clipboard bitmap capture

`Graphics.CopyFromScreen` captures screen coordinates, NOT the window's own content — if another window is on top, you capture that other window. Use Win32 `PrintWindow` with `PW_RENDERFULLCONTENT = 2` flag. Captures the window's own device context. Works even when the window is partially off-screen or covered.

### Avalonia 12 clipboard API

Use `RenderTargetBitmap.Render(visual)` + `ClipboardExtensions.SetBitmapAsync`, not GDI. The visual tree render works regardless of window state (maximised, minimised-then-restored, multi-monitor). Bitmap lands on the Windows clipboard in all formats simultaneously (`image/png`, `PNG`, `DeviceIndependentBitmap`, `Format17`, `Bitmap`) so it pastes into Slack/Discord/Word/Paint without fuss.

## Workflow gotchas

### Commit message temp files — `.gitignore` them

`COMMIT_MSG.tmp` slipped into the v1.8.0 commit because the staging step (`git add -A`) ran before the cleanup step. `.gitignore` now has:

```
COMMIT_MSG.tmp
*.commit.tmp
```

Better: write commit messages to `$env:TEMP`, which is outside the repo tree entirely.

### Screenshot workflow

For fresh ROADMAP screenshots:

1. Kill running launcher
2. `dotnet build` (confirms 0 warnings)
3. Start detached: `Start-Process -FilePath "dotnet" -ArgumentList 'run','--no-build','-c','Debug'`
4. Sleep 10s for Avalonia startup
5. Restore + size via `SetWindowPos` — maximised windows may report coords on a different monitor
6. `PrintWindow` with flag=2 into a `Bitmap`, save PNG to `docs/screenshots/photo_YYYY-MM-DD_vX.Y.Z.png`
7. Copy to Claude via `Filesystem:copy_file_user_to_claude` and view it to confirm the right window was captured
8. If the screenshot is wrong content: it means the window was hidden under another. Explicitly `SetForegroundWindow` + 500ms sleep, retry.

### UI Automation for launcher verification

```powershell
Add-Type -AssemblyName UIAutomationClient
$proc = Get-Process -Id <launcher-pid>
$root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
$texts = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Text)))
$texts | ForEach-Object { "  '$($_.Current.Name)'" }
```

This reads the actual rendered text, which is what the user sees — far more reliable than inferring from XAML.

### Release checklist

Before every tag:

- [ ] Kill running launcher
- [ ] `dotnet build` — 0 warnings, 0 errors
- [ ] `dotnet test --nologo --verbosity minimal` — 100% pass, zero skips
- [ ] Line-count audit (see command above) — no files > 200 lines
- [ ] Bump `<Version>` + `<AssemblyVersion>` in csproj
- [ ] ROADMAP.md — move current work from "Planned" to "Released", with design rationale comments
- [ ] Fresh screenshot referenced in the ROADMAP section
- [ ] `git status` — no stray files like `COMMIT_MSG.tmp`
- [ ] Commit via `git commit -F $env:TEMP\commit-msg.txt`
- [ ] `git tag vX.Y.Z`
- [ ] `git push origin master` then `git push origin vX.Y.Z`
- [ ] Poll `gh run list --limit 1` every 30-60s until green (~2m)
- [ ] `gh release view vX.Y.Z` — confirm both `.exe` and `.zip` assets published

### Extension install errors that aren't errors

If Claude's extension page shows `ENOTEMPTY: rmdir .../readable-stream`, check if the user is trying to install Desktop Commander. That extension is a **2.2 GB compressed MCPB download** (Puppeteer + Chromium + ffmpeg bundle). Claude's installer shows no progress UI and takes ~7-10 minutes. The "error" is almost always the user cancelling + retrying, leaving a half-deleted node_modules tree that a second install attempt can't clean up.

Recovery: close the slot fully (including tray icon), delete the whole `ant.dir.gh.wonderwhy-er.desktopcommandermcp` folder, reopen slot, reinstall, wait 10 minutes.

Download progress lives at `%LOCALAPPDATA%\ClaudeSlotN\logs\main.log` — grep `[download:`.

## CoODL architecture invariants

- `MainWindowViewModel` depends only on `I<Service>` abstractions. No direct WMI / filesystem / registry calls.
- Core layer (`ComeOnOverDesktopLauncher.Core`) has zero Avalonia references. Keep UI out of Core.
- Every service has an `I<Service>` interface in `Services/Interfaces/` and a concrete implementation in `Services/`.
- List VMs own reconciliation — they mutate their `ObservableCollection` in place, never reassign. Preserves Avalonia binding identity so row state (edit-in-progress text) survives refreshes.
- Reconciliation is identity-preserving: match by stable key (slot number for slots, PID for externals), update in place, add missing, remove gone. Never clear-and-repopulate.
- Pure functions / testable logic separated from IO-orchestration services. Example: `SeedCacheValidators` (pure validation) extracted from `FileSlotSeedCache` (IO orchestration).
- Scanner→Classifier→ViewModel pipeline. Scanner returns raw `ClaudeProcessInfo`. Classifier returns typed `SlotProcessInfo` / `ExternalProcessInfo` / null. VMs filter + reconcile.

## Memory / mental model

- The user runs the CoODL launcher on Windows (Dell laptop is Linux but not the target).
- ClaudeSlot{N} data directories live at `%LOCALAPPDATA%\ClaudeSlot{N}\`.
- Seed cache is at `%APPDATA%\ComeOnOverDesktopLauncher\seed\`.
- Logs are at `%APPDATA%\ComeOnOverDesktopLauncher\logs\launcher-YYYY-MM-DD.log`.
- The user is on mobile often but this specific project is exclusively Windows-local (not GitHub API).
- User's Windows GitHub Desktop is the shiftkey fork v3.4.12-linux1.
- AutoHotkey v2 script at `C:\Users\Lewis\Documents\AutoHotkey\Continue.ahk`.
