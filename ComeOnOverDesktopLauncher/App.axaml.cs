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
    private ISlotProcessMonitor? _slotProcessMonitor;
    private SlotSeedCacheUpdater? _seedCacheUpdater;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _serviceProvider = ConfigureServices().BuildServiceProvider();
        _serviceProvider.GetRequiredService<IClaudePathCache>().Refresh();

        // Self-heal any Start Menu shortcut the Velopack updater may
        // have left empty during a previous update apply. See
        // docs/dev/VELOPACK.md for the upstream bug this works around.
        // Runs early but after DI so logging is wired up; runs before
        // MainWindow is shown so it doesn't race with user activity.
        _serviceProvider.GetRequiredService<IShortcutHealer>().HealIfMissing();

        _slotProcessMonitor = _serviceProvider.GetRequiredService<ISlotProcessMonitor>();
        _slotProcessMonitor.Start(TimeSpan.FromSeconds(2));
        _seedCacheUpdater = _serviceProvider.GetRequiredService<SlotSeedCacheUpdater>();
        _seedCacheUpdater.Start();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var startMinimised = desktop.Args?.Contains("--minimised") == true;
            var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
            _mainWindow = new MainWindow
            {
                DataContext = viewModel,
                SnapshotService = _serviceProvider.GetRequiredService<IWindowSnapshotService>()
            };

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
                _seedCacheUpdater?.Stop();
                _slotProcessMonitor?.Stop();
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
        services.AddSingleton<ILoggingService, FileLoggingService>();
        services.AddSingleton<IProcessService, SystemProcessService>();
        services.AddSingleton<IClaudeProcessScanner, WmiClaudeProcessScanner>();
        services.AddSingleton<IClaudeProcessClassifier, RegexClaudeProcessClassifier>();
        services.AddSingleton<IWindowThumbnailService, PrintWindowThumbnailService>();
        services.AddSingleton<IThumbnailPreviewService, AvaloniaThumbnailPreviewService>();
        services.AddSingleton<IRegistryService, WindowsRegistryService>();
        services.AddSingleton<IClaudePathResolver, ClaudePathResolver>();
        services.AddSingleton<IClaudePathCache, ClaudePathCache>();
        services.AddSingleton<IClaudeVersionResolver, ClaudeVersionResolver>();
        services.AddSingleton<IClaudeInstanceLauncher, ClaudeInstanceLauncher>();
        services.AddSingleton<ISlotManager, SlotManager>();
        services.AddSingleton<ISlotSeedCache, FileSlotSeedCache>();
        services.AddSingleton<ISlotProcessMonitor, SlotProcessMonitor>();
        services.AddSingleton<SlotSeedCacheUpdater>();
        services.AddSingleton<ISlotInitialiser, SlotInitialiser>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton(provider => provider.GetRequiredService<ISettingsService>().Load());
        services.AddSingleton<IComeOnOverAppService, ComeOnOverAppService>();
        services.AddSingleton<IResourceMonitor, ResourceMonitor>();
        services.AddSingleton<IStartupService, StartupService>();
        services.AddSingleton<IVersionProvider, VersionProvider>();
        services.AddSingleton<IAutoUpdateService, VelopackAutoUpdateService>();
        services.AddSingleton<IShellLinkWriter, WScriptShellLinkWriter>();
        services.AddSingleton<IIconCacheRefresher, WindowsIconCacheRefresher>();
        services.AddSingleton<IShortcutHealer, WindowsShortcutHealer>();
        services.AddSingleton<ITrayIconService, TrayIconService>();
        services.AddSingleton<IWindowSnapshotService, WindowSnapshotService>();
        services.AddSingleton<IConfirmDialogService, ConfirmDialogService>();
        services.AddTransient<SlotInstanceListViewModel>();
        services.AddTransient<ExternalInstanceListViewModel>();
        services.AddTransient<MainWindowViewModel>();

        return services;
    }
}




