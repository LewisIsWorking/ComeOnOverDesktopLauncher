using System.Collections.ObjectModel;
using System.Reflection;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.ViewModels;

/// <summary>
/// Drives the main launcher window.
/// Handles launching Claude instances, resource monitoring, startup toggle, update checks and ComeOnOver.
/// Every launch attempt is logged via <see cref="ILoggingService"/> to aid diagnosis of silent failures.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IClaudeInstanceLauncher _launcher;
    private readonly ISlotManager _slotManager;
    private readonly ISlotInitialiser _slotInitialiser;
    private readonly IComeOnOverAppService _cooService;
    private readonly ISettingsService _settingsService;
    private readonly IResourceMonitor _resourceMonitor;
    private readonly IStartupService _startupService;
    private readonly IUpdateChecker _updateChecker;
    private readonly IVersionProvider _versionProvider;
    private readonly IProcessService _processService;
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

    public string AppVersion { get; }
    public bool HasRunningInstances => RunningInstanceCount > 0;
    public ObservableCollection<ClaudeInstanceViewModel> Instances { get; } = new();

    public MainWindowViewModel(
        IClaudeInstanceLauncher launcher,
        ISlotManager slotManager,
        ISlotInitialiser slotInitialiser,
        IComeOnOverAppService cooService,
        ISettingsService settingsService,
        IClaudePathResolver pathResolver,
        IResourceMonitor resourceMonitor,
        IStartupService startupService,
        IUpdateChecker updateChecker,
        IVersionProvider versionProvider,
        IProcessService processService,
        ILoggingService logger)
    {
        _launcher = launcher;
        _slotManager = slotManager;
        _slotInitialiser = slotInitialiser;
        _cooService = cooService;
        _settingsService = settingsService;
        _resourceMonitor = resourceMonitor;
        _startupService = startupService;
        _updateChecker = updateChecker;
        _versionProvider = versionProvider;
        _processService = processService;
        _logger = logger;

        AppVersion = $"v{versionProvider.GetVersion()}";
        _settings = _settingsService.Load();
        _slotCount = _settings.DefaultSlotCount;
        _refreshIntervalSeconds = _settings.ResourceRefreshIntervalSeconds;
        _launchOnStartup = _startupService.IsStartupEnabled();
        _isClaudeInstalled = pathResolver.IsClaudeInstalled();
        _runningInstanceCount = _launcher.GetRunningInstanceCount();

        _refreshTimer = new DispatcherTimer
            { Interval = TimeSpan.FromSeconds(_refreshIntervalSeconds) };
        _refreshTimer.Tick += (_, _) => RefreshResources();
        _refreshTimer.Start();
        _logger.LogInfo($"MainWindowViewModel ready. ClaudeInstalled={_isClaudeInstalled}, SlotCount={_slotCount}");
        _ = CheckForUpdatesAsync();
    }

    partial void OnLaunchOnStartupChanged(bool value)
    {
        if (value)
        {
            var exePath = Assembly.GetEntryAssembly()?.Location ?? string.Empty;
            _startupService.EnableStartup(exePath);
        }
        else
        {
            _startupService.DisableStartup();
        }
    }

    partial void OnRefreshIntervalSecondsChanged(int value)
    {
        if (value < 1) return;
        _refreshTimer.Interval = TimeSpan.FromSeconds(value);
        _settings.ResourceRefreshIntervalSeconds = value;
        SaveSettings();
    }

    [RelayCommand]
    private void LaunchInstances()
    {
        _logger.LogInfo($"LaunchInstances command fired - SlotCount={SlotCount}");
        try
        {
            var slots = _slotManager.GetNextFreeSlots(SlotCount);
            _logger.LogInfo(
                $"Picked free slot(s): {string.Join(", ", slots.Select(s => s.SlotNumber))}");
            foreach (var slot in slots)
            {
                _slotInitialiser.EnsureInitialised(slot);
                _launcher.LaunchSlot(slot);
            }
            RunningInstanceCount = _launcher.GetRunningInstanceCount();
            StatusMessage = $"Launched {SlotCount} instance(s). {RunningInstanceCount} running.";
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
        SyncInstanceCollection(snapshots);
    }

    [RelayCommand]
    private async Task CheckForUpdates()
    {
        await CheckForUpdatesAsync();
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settings.DefaultSlotCount = SlotCount;
        _settingsService.Save(_settings);
    }

    private async Task CheckForUpdatesAsync()
    {
        var latest = await _updateChecker.GetLatestVersionAsync();
        if (latest is null) return;
        var current = _versionProvider.GetVersion();
        UpdateAvailableMessage = _updateChecker.IsNewerVersion(current, latest)
            ? $"v{latest} available at github.com/LewisIsWorking/ComeOnOverDesktopLauncher"
            : null;
    }

    private void OnKillInstance(int processId)
    {
        _launcher.KillInstance(processId);
        RefreshResources();
    }

    private void OnSlotNameChanged(int slotNumber, string name)
    {
        _settings.SlotNames[slotNumber] = name;
        SaveSettings();
    }

    private void SyncInstanceCollection(IReadOnlyList<InstanceResourceSnapshot> snapshots)
    {
        while (Instances.Count > snapshots.Count)
            Instances.RemoveAt(Instances.Count - 1);

        for (var i = 0; i < snapshots.Count; i++)
        {
            if (i >= Instances.Count)
            {
                var num = snapshots[i].InstanceNumber;
                var slot = new LaunchSlot(num);
                Instances.Add(new ClaudeInstanceViewModel(
                    num,
                    _settings.GetSlotName(num),
                    _slotInitialiser.IsSeeded(slot),
                    OnSlotNameChanged,
                    OnKillInstance));
            }
            Instances[i].UpdateFrom(snapshots[i]);
        }
    }
}
