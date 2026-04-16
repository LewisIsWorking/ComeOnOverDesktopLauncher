using System;
using System.Runtime.Versioning;
using Avalonia;

namespace ComeOnOverDesktopLauncher;

/// <summary>
/// Entry point. Marked Windows-only because the app uses Windows-specific APIs
/// (registry for startup, MSIX package queries for Claude path resolution).
/// When cross-platform support is added in v2.0, platform-specific implementations
/// behind IRegistryService and IClaudePathResolver will remove this requirement.
/// </summary>
[SupportedOSPlatform("windows")]
sealed class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
