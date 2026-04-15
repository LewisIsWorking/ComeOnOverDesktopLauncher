using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ComeOnOverDesktopLauncher.Core.Services;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;
using ComeOnOverDesktopLauncher.ViewModels;
using ComeOnOverDesktopLauncher.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ComeOnOverDesktopLauncher;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _serviceProvider = ConfigureServices().BuildServiceProvider();

        // Refresh Claude path on every launch to handle Claude updates silently
        _serviceProvider.GetRequiredService<IClaudePathCache>().Refresh();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceCollection ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IFileSystem, WindowsFileSystem>();
        services.AddSingleton<IProcessService, SystemProcessService>();
        services.AddSingleton<IClaudePathResolver, ClaudePathResolver>();
        services.AddSingleton<IClaudePathCache, ClaudePathCache>();
        services.AddSingleton<IClaudeInstanceLauncher, ClaudeInstanceLauncher>();
        services.AddSingleton<ISlotManager, SlotManager>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton(provider => provider.GetRequiredService<ISettingsService>().Load());
        services.AddSingleton<IComeOnOverAppService, ComeOnOverAppService>();
        services.AddTransient<MainWindowViewModel>();

        return services;
    }
}
