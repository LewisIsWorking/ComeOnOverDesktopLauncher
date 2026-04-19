using System.Reflection;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.ViewModels;

/// <summary>
/// Drives the main launcher window.
/// Handles launching Claude instances, resource monitoring, startup
/// toggle, update checks and ComeOnOver.
///
/// Delegates the slot list to <see cref="SlotInstanceListViewModel"/>
/// and the external list to <see cref="ExternalInstanceListViewModel"/>
/// so that each windowed Claude process appears in exactly one list
/// (launcher-managed vs externally-launched).
///
/// Launch sequencing (slot picking + seeding + process start) lives
/// inside <see cref="IClaudeInstanceLauncher.LaunchInstances"/> so this
/// VM stays focused on UI orchestration.
///
/// Every launch attempt is logged via <see cref="ILoggingService"/> to
/// aid diagnosis of silent failures.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IClaudeInstanceLauncher _launcher;
    private readonly IComeOnOverAppService _cooService;
    private readonly ISettingsService _settingsService;
    private readonly IResourceMonitor _resourceMonitor;
    private readonly IStartupService _startupService;
    private readonly IUpdateNotifier _updateNotifier;
    private readonly IProcessService _processService;
    private readonly IWindowThumbnailService _thumbnailService;
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
    [ObservableProperty] private string? _updateAvailableMessage;
    [ObservableProperty] private bool _thumbnailsEnabled;

    public string AppVersion { get; }
    public string? ClaudeVersion { get; }
    public string FooterVersionText =>
        ClaudeVersion is null ? AppVersion : $"{AppVersion} - Claude {ClaudeVersion}";
    public bool HasRunningInstances => RunningInstanceCount > 0;
    public SlotInstanceListViewModel SlotInstances { get; }
    public ExternalInstanceListViewModel ExternalInstances { get; }

    public MainWindowViewModel(
        IClaudeInstanceLauncher launcher,
        IComeOnOverAppService cooService,
        ISettingsService settingsService,
        IClaudePathResolver pathResolver,
        IResourceMonitor resourceMonitor,
        IStartupService startupService,
        IUpdateNotifier updateNotifier,
        IVersionProvider versionProvider,
        IClaudeVersionResolver claudeVersionResolver,
        IProcessService processService,
        IWindowThumbnailService thumbnailService,
        SlotInstanceListViewModel slotInstances,
        ExternalInstanceListViewModel externalInstances,
        ILoggingService logger)
    {
        _launcher = launcher;
        _cooService = cooService;
        _settingsService = settingsService;
        _resourceMonitor = resourceMonitor;
        _startupService = startupService;
        _updateNotifier = updateNotifier;
        _processService = processService;
        _thumbnailService = thumbnailService;
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

        SlotCallbackBinder.Bind(SlotInstances, _settings, _launcher, SaveSettings, RefreshResources);

        _refreshTimer = new DispatcherTimer
            { Interval = TimeSpan.FromSeconds(_refreshIntervalSeconds) };
        _refreshTimer.Tick += (_, _) => RefreshResources();
        _refreshTimer.Start();
        _logger.LogInfo($"MainWindowViewModel ready. ClaudeInstalled={_isClaudeInstalled}, SlotCount={_slotCount}");
        _ = CheckForUpdates();
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

    // When the user toggles the "Show thumbnails" checkbox the
    // ThumbnailRefresher handles settings persistence + clearing stale
    // captures if the toggle went off. See ThumbnailRefresher.HandleToggleChange
    // for the full rationale (including why v1.9.0 only covers slot
    // collections and externals join in v1.9.1).
    partial void OnThumbnailsEnabledChanged(bool value) =>
        ThumbnailRefresher.HandleToggleChange(
            value, _settings, SaveSettings,
            SlotInstances.Items, SlotInstances.TrayItems);

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
            // v1.9.0 covers slot thumbnails only. External instances
            // will join in v1.9.1 alongside the grid card migration.
            ThumbnailRefresher.RefreshVisibleThumbnails(
                _thumbnailService, SlotInstances.Items, 240, 150);
        }
    }

    [RelayCommand]
    private async Task CheckForUpdates() =>
        UpdateAvailableMessage = await _updateNotifier.GetUpdateAvailableMessageAsync();

    [RelayCommand]
    private void SaveSettings()
    {
        _settings.DefaultSlotCount = SlotCount;
        _settingsService.Save(_settings);
    }
}
