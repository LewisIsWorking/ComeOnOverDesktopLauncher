using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;
using ComeOnOverDesktopLauncher.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.ViewModels;

/// <summary>
/// Drives the main launcher window.
/// Handles launching Claude instances, startup toggle, and the
/// ComeOnOver/Logs commands.
///
/// <para>
/// Delegates per-concern state to four sub-VMs so this root stays
/// well under the 200-line limit:
/// <list type="bullet">
///   <item><see cref="SlotInstances"/> - launcher-managed slot rows</item>
///   <item><see cref="ExternalInstances"/> - externally-launched Claude rows</item>
///   <item><see cref="Update"/> - Velopack update banner state</item>
///   <item><see cref="Resources"/> - running count, total RAM/CPU, refresh timer, thumbnail capture</item>
/// </list>
/// XAML bindings that used to target root properties like
/// <c>TotalRamMb</c>, <c>RunningInstanceCount</c>, or
/// <c>HasRunningInstances</c> now go through
/// <c>Resources.TotalRamMb</c> etc. The v1.10.6 extraction was
/// forced by the root hitting 200 lines after v1.10.5's Hide
/// feature; per the ROADMAP handoff rule, the refactor ran ahead
/// of any new feature work so future changes inherit the headroom.
/// </para>
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IClaudeInstanceLauncher _launcher;
    private readonly IComeOnOverAppService _cooService;
    private readonly ISettingsService _settingsService;
    private readonly IStartupService _startupService;
    private readonly IProcessService _processService;
    private readonly ILoggingService _logger;
    private AppSettings _settings;

    [ObservableProperty] private int _slotCount;
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private bool _isClaudeInstalled;
    [ObservableProperty] private bool _launchOnStartup;
    [ObservableProperty] private bool _thumbnailsEnabled;

    public string AppVersion { get; }
    public string? ClaudeVersion { get; }
    public string FooterVersionText =>
        ClaudeVersion is null ? AppVersion : $"{AppVersion} - Claude {ClaudeVersion}";
    public SlotInstanceListViewModel SlotInstances { get; }
    public ExternalInstanceListViewModel ExternalInstances { get; }
    public MainWindowUpdateViewModel Update { get; }
    public MainWindowResourceViewModel Resources { get; }

    public MainWindowViewModel(
        IClaudeInstanceLauncher launcher,
        IComeOnOverAppService cooService,
        ISettingsService settingsService,
        IClaudePathResolver pathResolver,
        IResourceMonitor resourceMonitor,
        IStartupService startupService,
        IAutoUpdateService autoUpdateService,
        IUpdateApplyFailureDetector applyFailureDetector,
        IVersionProvider versionProvider,
        IClaudeVersionResolver claudeVersionResolver,
        IProcessService processService,
        IWindowThumbnailService thumbnailService,
        IThumbnailPreviewService previewService,
        IWindowHider windowHider,
        SlotInstanceListViewModel slotInstances,
        ExternalInstanceListViewModel externalInstances,
        ILoggingService logger)
    {
        _launcher = launcher;
        _cooService = cooService;
        _settingsService = settingsService;
        _startupService = startupService;
        _processService = processService;
        SlotInstances = slotInstances;
        ExternalInstances = externalInstances;
        _logger = logger;

        AppVersion = $"v{versionProvider.GetVersion()}";
        ClaudeVersion = claudeVersionResolver.GetClaudeVersion();
        _settings = _settingsService.Load();
        _slotCount = _settings.DefaultSlotCount;
        _thumbnailsEnabled = _settings.ThumbnailsEnabled;
        _launchOnStartup = _startupService.IsStartupEnabled();
        _isClaudeInstalled = pathResolver.IsClaudeInstalled();

        Resources = new MainWindowResourceViewModel(
            _launcher, resourceMonitor, thumbnailService,
            SlotInstances, ExternalInstances,
            _settings.ResourceRefreshIntervalSeconds,
            () => ThumbnailsEnabled,
            onIntervalChanged: () =>
            {
                _settings.ResourceRefreshIntervalSeconds = Resources.IntervalSeconds;
                SaveSettings();
            });

        SlotCallbackBinder.Bind(SlotInstances, _settings, _launcher, windowHider, previewService, SaveSettings, Resources.Refresh);
        SlotCallbackBinder.BindExternal(ExternalInstances, previewService);

        Update = new MainWindowUpdateViewModel(
            autoUpdateService, _processService, _logger, _settings,
            applyFailureDetector, SaveSettings);

        _logger.LogInfo($"MainWindowViewModel ready. ClaudeInstalled={_isClaudeInstalled}, SlotCount={_slotCount}");
    }

    partial void OnLaunchOnStartupChanged(bool value)
    {
        if (value)
            _startupService.EnableStartup(Assembly.GetEntryAssembly()?.Location ?? string.Empty);
        else
            _startupService.DisableStartup();
    }

    // Delegates both settings persistence and (on disable) thumbnail
    // clearing to ThumbnailRefresher. The overload that takes concrete
    // collection types handles the Cast+Concat internally so this call
    // site stays single-line.
    partial void OnThumbnailsEnabledChanged(bool value) =>
        ThumbnailRefresher.HandleToggleChange(
            value, _settings, SaveSettings,
            SlotInstances.Items, SlotInstances.TrayItems, ExternalInstances.Items);

    [RelayCommand]
    private void LaunchInstances()
    {
        _logger.LogInfo($"LaunchInstances command fired - SlotCount={SlotCount}");
        try
        {
            var launched = _launcher.LaunchInstances(SlotCount);
            Resources.Refresh();
            StatusMessage = $"Launched {launched.Count} instance(s). {Resources.RunningInstanceCount} running.";
            _logger.LogInfo($"LaunchInstances finished - {Resources.RunningInstanceCount} running");
            SaveSettings();
        }
        catch (Exception ex)
        {
            _logger.LogError("LaunchInstances failed", ex);
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void LaunchComeOnOver()
    {
        _cooService.Launch();
        StatusMessage = "ComeOnOver opened.";
    }

    [RelayCommand]
    private void OpenLogsFolder()
    {
        _processService.Start("explorer.exe", _logger.GetLogDirectory(), useShellExecute: false);
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settings.DefaultSlotCount = SlotCount;
        _settingsService.Save(_settings);
    }
}
