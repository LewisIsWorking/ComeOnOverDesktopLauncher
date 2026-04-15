using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.ViewModels;

/// <summary>
/// Drives the main launcher window.
/// Handles launching Claude instances, resource monitoring, and the ComeOnOver app.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IClaudeInstanceLauncher _launcher;
    private readonly ISlotManager _slotManager;
    private readonly IComeOnOverAppService _cooService;
    private readonly ISettingsService _settingsService;
    private readonly IResourceMonitor _resourceMonitor;
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

    public bool HasRunningInstances => RunningInstanceCount > 0;
    public ObservableCollection<ClaudeInstanceViewModel> Instances { get; } = new();

    public MainWindowViewModel(
        IClaudeInstanceLauncher launcher,
        ISlotManager slotManager,
        IComeOnOverAppService cooService,
        ISettingsService settingsService,
        IClaudePathResolver pathResolver,
        IResourceMonitor resourceMonitor)
    {
        _launcher = launcher;
        _slotManager = slotManager;
        _cooService = cooService;
        _settingsService = settingsService;
        _resourceMonitor = resourceMonitor;

        _settings = _settingsService.Load();
        _slotCount = _settings.DefaultSlotCount;
        _isClaudeInstalled = pathResolver.IsClaudeInstalled();
        _runningInstanceCount = _launcher.GetRunningInstanceCount();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _refreshTimer.Tick += (_, _) => RefreshResources();
        _refreshTimer.Start();
    }

    [RelayCommand]
    private void LaunchInstances()
    {
        try
        {
            var slots = _slotManager.GetSlots(SlotCount);
            foreach (var slot in slots)
                _launcher.LaunchSlot(slot);

            RunningInstanceCount = _launcher.GetRunningInstanceCount();
            StatusMessage = $"Launched {SlotCount} instance(s). {RunningInstanceCount} running.";
            SaveSettings();
        }
        catch (Exception ex)
        {
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
    private void RefreshResources()
    {
        RunningInstanceCount = _launcher.GetRunningInstanceCount();
        var snapshots = _resourceMonitor.GetSnapshots();
        TotalRamMb = _resourceMonitor.TotalRamMb;
        TotalCpuPercent = _resourceMonitor.TotalCpuPercent;
        SyncInstanceCollection(snapshots);
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settings.DefaultSlotCount = SlotCount;
        _settingsService.Save(_settings);
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
                Instances.Add(new ClaudeInstanceViewModel(
                    num,
                    _settings.GetSlotName(num),
                    OnSlotNameChanged));
            }

            Instances[i].UpdateFrom(snapshots[i]);
        }
    }
}
