using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ComeOnOverDesktopLauncher.Core.Services;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;
using ComeOnOverDesktopLauncher.Services;
using ComeOnOverDesktopLauncher.Services.Interfaces;
using ComeOnOverDesktopLauncher.ViewModels;
using ComeOnOverDesktopLauncher.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ComeOnOverDesktopLauncher;

[SupportedOSPlatform("windows")]
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private MainWindow? _mainWindow;
    private ITrayIconService? _trayIconService;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _serviceProvider = ConfigureServices().BuildServiceProvider();
        _serviceProvider.GetRequiredService<IClaudePathCache>().Refresh();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var startMinimised = desktop.Args?.Contains("--minimised") == true;
            var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
            _mainWindow = new MainWindow { DataContext = viewModel };

            desktop.MainWindow = _mainWindow;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            SetupTrayIcon(viewModel, desktop);

            if (!startMinimised)
                _mainWindow.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(MainWindowViewModel viewModel, IClassicDesktopStyleApplicationLifetime desktop)
    {
        _trayIconService = _serviceProvider!.GetRequiredService<ITrayIconService>();
        _trayIconService.Initialise(
            onShow: ShowMainWindow,
            onLaunchClaude: () => viewModel.LaunchInstancesCommand.Execute(null),
            onQuit: () =>
            {
                _trayIconService.Dispose();
                desktop.Shutdown();
            });
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    [SupportedOSPlatform("windows")]
    private static ServiceCollection ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IFileSystem, WindowsFileSystem>();
        services.AddSingleton<IProcessService, SystemProcessService>();
        services.AddSingleton<IRegistryService, WindowsRegistryService>();
        services.AddSingleton<IClaudePathResolver, ClaudePathResolver>();
        services.AddSingleton<IClaudePathCache, ClaudePathCache>();
        services.AddSingleton<IClaudeInstanceLauncher, ClaudeInstanceLauncher>();
        services.AddSingleton<ISlotManager, SlotManager>();
        services.AddSingleton<ISlotInitialiser, SlotInitialiser>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton(provider => provider.GetRequiredService<ISettingsService>().Load());
        services.AddSingleton<IComeOnOverAppService, ComeOnOverAppService>();
        services.AddSingleton<IResourceMonitor, ResourceMonitor>();
        services.AddSingleton<IStartupService, StartupService>();
        services.AddSingleton<IVersionProvider, VersionProvider>();
        services.AddSingleton<ITrayIconService, TrayIconService>();
        services.AddTransient<MainWindowViewModel>();

        return services;
    }
}


