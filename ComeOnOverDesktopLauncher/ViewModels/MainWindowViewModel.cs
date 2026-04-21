using System.Reflection;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;
using ComeOnOverDesktopLauncher.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.ViewModels;

/// <summary>
/// Drives the main launcher window.
/// Handles launching Claude instances, resource monitoring, startup
/// toggle, update checks and ComeOnOver.
///
/// Delegates the slot list to <see cref="SlotInstanceListViewModel"/>,
/// the external list to <see cref="ExternalInstanceListViewModel"/>,
/// and the update banner state to <see cref="MainWindowUpdateViewModel"/>
/// so this VM stays under the 200-line limit and each concern lives
/// in its own testable unit.
///
/// Launch sequencing lives inside <see cref="IClaudeInstanceLauncher.LaunchInstances"/>
/// so this VM stays focused on UI orchestration. Every launch attempt
/// is logged via <see cref="ILoggingService"/> to aid diagnosis of
/// silent failures.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IClaudeInstanceLauncher _launcher;
    private readonly IComeOnOverAppService _cooService;
    private readonly ISettingsService _settingsService;
    private readonly IResourceMonitor _resourceMonitor;
    private readonly IStartupService _startupService;
    private readonly IProcessService _processService;
    private readonly IWindowThumbnailService _thumbnailService;
    private readonly IThumbnailPreviewService _previewService;
    private readonly ILoggingService _logger;
    private readonly DispatcherTimer _refreshTimer;
    private AppSettings _settings;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRunningInstances))]
    private int _runningInstanceCount;

    [ObservableProperty] private int _slotCount;
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private bool _isClaudeInstalled;
    [ObservableProperty] private double _totalRamMb;
    [ObservableProperty] private double _totalCpuPercent;
    [ObservableProperty] private bool _launchOnStartup;
    [ObservableProperty] private int _refreshIntervalSeconds;
    [ObservableProperty] private bool _thumbnailsEnabled;

    public string AppVersion { get; }
    public string? ClaudeVersion { get; }
    public string FooterVersionText =>
        ClaudeVersion is null ? AppVersion : $"{AppVersion} - Claude {ClaudeVersion}";
    public bool HasRunningInstances => RunningInstanceCount > 0;
    public SlotInstanceListViewModel SlotInstances { get; }
    public ExternalInstanceListViewModel ExternalInstances { get; }
    public MainWindowUpdateViewModel Update { get; }

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
        _resourceMonitor = resourceMonitor;
        _startupService = startupService;
        _processService = processService;
        _thumbnailService = thumbnailService;
        _previewService = previewService;
        SlotInstances = slotInstances;
        ExternalInstances = externalInstances;
        _logger = logger;

        AppVersion = $"v{versionProvider.GetVersion()}";
        ClaudeVersion = claudeVersionResolver.GetClaudeVersion();
        _settings = _settingsService.Load();
        _slotCount = _settings.DefaultSlotCount;
        _refreshIntervalSeconds = _settings.ResourceRefreshIntervalSeconds;
        _thumbnailsEnabled = _settings.ThumbnailsEnabled;
        _launchOnStartup = _startupService.IsStartupEnabled();
        _isClaudeInstalled = pathResolver.IsClaudeInstalled();
        _runningInstanceCount = _launcher.GetRunningInstanceCount();

        SlotCallbackBinder.Bind(SlotInstances, _settings, _launcher, windowHider, _previewService, SaveSettings, RefreshResources);
        SlotCallbackBinder.BindExternal(ExternalInstances, _previewService);

        Update = new MainWindowUpdateViewModel(
            autoUpdateService, _processService, _logger, _settings,
            applyFailureDetector, SaveSettings);

        _refreshTimer = new DispatcherTimer
            { Interval = TimeSpan.FromSeconds(_refreshIntervalSeconds) };
        _refreshTimer.Tick += (_, _) => RefreshResources();
        _refreshTimer.Start();
        _logger.LogInfo($"MainWindowViewModel ready. ClaudeInstalled={_isClaudeInstalled}, SlotCount={_slotCount}");
    }

    partial void OnLaunchOnStartupChanged(bool value)
    {
        if (value)
            _startupService.EnableStartup(Assembly.GetEntryAssembly()?.Location ?? string.Empty);
        else
            _startupService.DisableStartup();
    }

    partial void OnRefreshIntervalSecondsChanged(int value)
    {
        if (value < 1) return;
        _refreshTimer.Interval = TimeSpan.FromSeconds(value);
        _settings.ResourceRefreshIntervalSeconds = value;
        SaveSettings();
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
            RunningInstanceCount = _launcher.GetRunningInstanceCount();
            StatusMessage = $"Launched {launched.Count} instance(s). {RunningInstanceCount} running.";
            _logger.LogInfo($"LaunchInstances finished - {RunningInstanceCount} running");
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
    private void RefreshResources()
    {
        RunningInstanceCount = _launcher.GetRunningInstanceCount();
        var snapshots = _resourceMonitor.GetSnapshots();
        TotalRamMb = _resourceMonitor.TotalRamMb;
        TotalCpuPercent = _resourceMonitor.TotalCpuPercent;
        SlotInstances.Refresh(snapshots);
        ExternalInstances.Refresh(snapshots);

        if (ThumbnailsEnabled)
        {
            ThumbnailRefresher.RefreshVisibleThumbnails(
                _thumbnailService, SlotInstances.Items, 240, 150);
            ThumbnailRefresher.RefreshVisibleThumbnails(
                _thumbnailService, ExternalInstances.Items, 240, 150);
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settings.DefaultSlotCount = SlotCount;
        _settingsService.Save(_settings);
    }
}
