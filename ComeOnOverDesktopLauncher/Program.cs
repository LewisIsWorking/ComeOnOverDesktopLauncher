using System;
using System.Runtime.Versioning;
using Avalonia;

namespace ComeOnOverDesktopLauncher;

sealed class Program
{
    [STAThread]
    [SupportedOSPlatform("windows")]
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
