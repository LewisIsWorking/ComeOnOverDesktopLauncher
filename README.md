# ComeOnOver Desktop Launcher

A launcher utility for the [ComeOnOver](https://comeonover.netlify.app) ecosystem.

## Features

- Launch one or more Claude Desktop instances simultaneously
- Each instance uses a fixed named slot, preserving your login session between launches
- Open the ComeOnOver web app directly from the launcher
- Settings persist between sessions

## Requirements

- Windows 10/11
- [Claude Desktop](https://claude.ai/download) installed via the Microsoft Store
- .NET 10 runtime

## Getting Started

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
- 200-line file limit — extract to new files rather than growing existing ones
- 100% unit test coverage required on all commits

## Roadmap

See [ROADMAP.md](ROADMAP.md) for planned features and release milestones.
