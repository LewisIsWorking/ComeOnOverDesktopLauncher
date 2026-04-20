# CoODL — Build & tooling gotchas

Extracted from `LEARNINGS.md` during v1.9.2 to keep the root file under the 200-line limit. These are accumulated gotchas from doing releases of this codebase.

## Line-count audit command

Run this any time you suspect drift. It's also what the CI guard (`FileSizeLimitTests`) checks:

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

## Kill the running launcher before rebuilding

```powershell
Get-Process -Name "ComeOnOverDesktopLauncher" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
```

If the launcher is running, `dotnet build` fails with `MSB3027: The file is locked by: "ComeOnOverDesktopLauncher (PID)"` because msbuild can't overwrite the DLL. Always kill before building after a live-run session.

## PowerShell / heredoc quirks in this codebase

- `$PID` is a read-only PowerShell automatic variable. Use `$procId` instead.
- Files use LF line endings (enforced by `.gitattributes`). When doing replacements with Set-Content, use `$LF = "`n"` rather than `"`r`n"`.
- Single-quoted heredoc `@'...'@` preserves C# `$"..."` interpolation. Double-quoted `@"..."@` eats the `$`.
- For multi-line git commit messages: **always** write to `$env:TEMP\*.txt` then `git commit -F <tempfile>`, never pipe a heredoc directly to `git commit -m`. Piping breaks on PowerShell line-continuation rules. Delete the temp file after.

## Working directory drift

Some `start_process` / `bash` calls spawn a subshell with `bin/Debug/` as the cwd (because the last process the current shell ran was in that directory). Always start commands with an explicit `Set-Location` or `cd` to the repo root. Symptom is `ReadAllLines` with paths like `bin\Debug\net10.0\ComeOnOverDesktopLauncher.Core\...`.

## CI runner gotchas

### ImageMagick

The GitHub Actions release workflow does NOT have ImageMagick installed. Always commit the pre-rasterised binaries (`.ico`, `.png`) alongside the SVG source. The SVG + `build-icons.ps1` live at `docs/design/` as the regeneration path.

### .NET 10 on the CI runner

Historically the workflow did a `regex net10.0 → net9.0` substitution before `dotnet publish` because `windows-latest` runners didn't have .NET 10 preinstalled. In v1.10.0 we simplified this: `actions/setup-dotnet@v4` supports `dotnet-version: '10.0.x'` directly — the action downloads and installs the SDK when it's not preinstalled. No regex hack needed. The build now targets `net10.0` end-to-end.

## Commit message temp files — `.gitignore` them

`COMMIT_MSG.tmp` slipped into the v1.8.0 commit because the staging step (`git add -A`) ran before the cleanup step. `.gitignore` now has:

```
COMMIT_MSG.tmp
*.commit.tmp
```

Better: write commit messages to `$env:TEMP`, which is outside the repo tree entirely.

## Screenshot workflow

For fresh ROADMAP screenshots:

1. Kill running launcher
2. `dotnet build` (confirms 0 warnings)
3. Start detached: `Start-Process -FilePath "dotnet" -ArgumentList 'run','--no-build','-c','Debug'`
4. Sleep 10s for Avalonia startup
5. Restore + size via `SetWindowPos` — maximised windows may report coords on a different monitor
6. `PrintWindow` with flag=2 into a `Bitmap`, save PNG to `docs/screenshots/photo_YYYY-MM-DD_vX.Y.Z.png`
7. Copy to Claude via `Filesystem:copy_file_user_to_claude` and view it to confirm the right window was captured
8. If the screenshot is wrong content: it means the window was hidden under another. Explicitly `SetForegroundWindow` + 500ms sleep, retry.

## UI Automation for launcher verification

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

## Release checklist

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

## Extension install errors that aren't errors

If Claude's extension page shows `ENOTEMPTY: rmdir .../readable-stream`, check if the user is trying to install Desktop Commander. That extension is a **2.2 GB compressed MCPB download** (Puppeteer + Chromium + ffmpeg bundle). Claude's installer shows no progress UI and takes ~7-10 minutes. The "error" is almost always the user cancelling + retrying, leaving a half-deleted node_modules tree that a second install attempt can't clean up.

Recovery: close the slot fully (including tray icon), delete the whole `ant.dir.gh.wonderwhy-er.desktopcommandermcp` folder, reopen slot, reinstall, wait 10 minutes.

Download progress lives at `%LOCALAPPDATA%\ClaudeSlotN\logs\main.log` — grep `[download:`.

## Velopack packaging (v1.10.0+)

CoODL releases are packaged with [Velopack](https://velopack.io). The CI workflow installs `vpk` via `dotnet tool install -g vpk`, then runs `vpk pack` to produce the `Setup.exe` installer + delta packages, then `vpk upload github` to publish the release.

### Local testing

For iterating on the install flow locally without pushing a tag:

```powershell
# From repo root
Remove-Item -Recurse -Force publish, Releases -ErrorAction SilentlyContinue

dotnet publish ComeOnOverDesktopLauncher\ComeOnOverDesktopLauncher.csproj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output publish `
    -p:PublishSingleFile=false

vpk pack `
    --packId ComeOnOverDesktopLauncher `
    --packVersion 1.10.0-test `
    --packDir publish `
    --mainExe ComeOnOverDesktopLauncher.exe `
    --packTitle "ComeOnOver Desktop Launcher" `
    --shortcuts "Desktop,StartMenu"
```

Output appears in `Releases/`. Key files:
- `ComeOnOverDesktopLauncher-win-Setup.exe` - bootstrap installer for end users
- `ComeOnOverDesktopLauncher-{version}-full.nupkg` - full Velopack update package
- `ComeOnOverDesktopLauncher-{version}-delta.nupkg` - delta from previous version (only from v1.10.1 onwards)
- `ComeOnOverDesktopLauncher-win-Portable.zip` - bonus portable variant
- `RELEASES`, `releases.win.json`, `assets.win.json` - Velopack manifests

Both `publish/` and `Releases/` are `.gitignore`d.

### Publish single-file MUST be false

Velopack needs a multi-file output to produce delta packages - it rebuilds individual assembly diffs between versions. The CI workflow's `dotnet publish` step explicitly sets `-p:PublishSingleFile=false`. Don't re-enable PublishSingleFile or you'll break delta generation and every update will be a full 100MB download.

### VelopackApp.Build() must run in Program.Main, not App.axaml.cs

`VelopackApp.Build().OnFirstRun(...).Run()` lives as the very first line of `Program.Main()`, before Avalonia's `BuildAvaloniaApp().StartWithClassicDesktopLifetime()`. If it runs later (e.g. inside `App.OnFrameworkInitializationCompleted`), `vpk pack` emits a warning and Velopack's install/update/uninstall hooks can briefly flash a window or fail silently. Don't move it.

### Code signing (future)

v1.10.0 ships unsigned. Every `vpk pack` emits `WRN: No signing parameters provided` for ~231 binaries - these warnings are expected and benign in the v1.10.0 era. Users see Windows SmartScreen on first install; subsequent auto-updates are silent (Velopack's updater inherits the install's trust).

When we do sign, **Azure Trusted Signing is the recommended option** (~$10/month, integrates cleanly with GitHub Actions, no hardware token). Traditional OV/EV certs require USB hardware tokens that don't work in CI. The retrofit is ~2 hours of workflow changes plus Azure tenant setup - a deliberately-deferred decision, not a forgotten one.

### Validating updates after shipping

Because the auto-update flow requires TWO published releases (the old one must actually be able to update to the new one), the canonical validation strategy is:
1. Ship v1.10.0 as the first Velopack release.
2. Install it locally from the published `Setup.exe`.
3. Ship v1.10.1 with a trivial change (e.g. a log message tweak) purely to validate the update pipeline.
4. Wait up to 6 hours (the poll interval) or restart the launcher to trigger the check.
5. Confirm the "Restart to install v1.10.1" banner appears, click it, verify the new version launches.

If any step fails, logs at `%APPDATA%\ComeOnOverDesktopLauncher\logs\launcher-YYYY-MM-DD.log` will show every `[Show]`, `[OpenWindow]`, `CheckForUpdatesAsync`, and Velopack-emitted entry.

### Why fetch-depth: 0 in the workflow

`vpk upload github` needs full git history to locate the previous release tag for delta package generation. Without `fetch-depth: 0` on `actions/checkout`, only the current tag is fetched and delta generation silently produces a full package instead. Delta packages are the whole point of the Velopack migration, so don't remove this.
