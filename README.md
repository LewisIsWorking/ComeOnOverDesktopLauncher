# ComeOnOver Desktop Launcher

A launcher utility for the [ComeOnOver](https://comeonover.netlify.app) ecosystem.

![ComeOnOver Desktop Launcher](docs/screenshots/photo_2026-04-15_16-13-35.jpg)

## Features

- Launch one or more Claude Desktop instances simultaneously
- Each instance uses a fixed named slot, preserving your login session between launches
- Name each instance (e.g. "Work", "Personal", "Research") - names persist between sessions
- Live CPU, RAM and uptime monitoring per instance and combined totals - auto-refreshes every 5 seconds
- Open the ComeOnOver web app directly from the launcher
- Minimises to system tray - always one click away

## Requirements

- Windows 10/11
- [Claude Desktop](https://claude.ai/download) installed via the Microsoft Store
- No .NET installation required - download the self-contained `.exe` from Releases

## Download

Head to [Releases](https://github.com/LewisIsWorking/ComeOnOverDesktopLauncher/releases) and download the latest `ComeOnOverDesktopLauncher-vX.X.X-win-x64.exe`.

## Getting Started (from source)

1. Clone the repository
2. Open `ComeOnOverDesktopLauncher.sln` in Rider or Visual Studio
3. Build and run `ComeOnOverDesktopLauncher`

## Project Structure

```
ComeOnOverDesktopLauncher/          # Avalonia UI (views, viewmodels, DI setup)
ComeOnOverDesktopLauncher.Core/     # Business logic (models, services, interfaces)
ComeOnOverDesktopLauncher.Tests/    # xUnit test suite (100% coverage)
```

## Development

- .NET 10, Avalonia 12, CommunityToolkit.Mvvm
- SOLID principles, MVVM pattern throughout
- 200-line file limit - extract to new files rather than growing existing ones
- 100% unit test coverage required on all commits

## Roadmap

See [ROADMAP.md](ROADMAP.md) for planned features and release milestones.
