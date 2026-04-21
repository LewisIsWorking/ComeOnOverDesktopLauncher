using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.ViewModels;

/// <summary>
/// Sub-VM for the resource-monitoring concern of the main window:
/// running instance count, total RAM/CPU, the refresh timer that
/// drives both, and the thumbnail capture triggered on every tick.
///
/// <para>
/// Extracted from <see cref="MainWindowViewModel"/> in v1.10.6 (after
/// the root VM hit its 200-line ceiling with v1.10.5). Same pattern
/// as <see cref="MainWindowUpdateViewModel"/>,
/// <see cref="SlotInstanceListViewModel"/>, and
/// <see cref="ExternalInstanceListViewModel"/> - single-concern
/// observable object that the root VM composes and XAML binds
/// through a dotted path (e.g. <c>Resources.TotalRamMb</c>).
/// </para>
///
/// <para>
/// Owns the <see cref="DispatcherTimer"/> rather than having the
/// root VM own it and call back. Ownership here keeps the
/// interval-changed wiring local: when <see cref="IntervalSeconds"/>
/// is set the timer re-configures itself, no round-trip through
/// the root VM needed.
/// </para>
///
/// <para>
/// <see cref="Refresh"/> is also exposed publicly because the root
/// VM's <see cref="SlotCallbackBinder"/> passes it as the
/// <c>refreshResources</c> callback - some per-row actions (notably
/// Kill) need to prod the resource pipeline immediately rather than
/// waiting for the next tick.
/// </para>
/// </summary>
public partial class MainWindowResourceViewModel : ObservableObject
{
    private readonly IResourceMonitor _resourceMonitor;
    private readonly IWindowThumbnailService _thumbnailService;
    private readonly SlotInstanceListViewModel _slotInstances;
    private readonly ExternalInstanceListViewModel _externalInstances;
    private readonly Func<bool> _getThumbnailsEnabled;
    private readonly Action _onIntervalChanged;
    private readonly DispatcherTimer _timer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRunningInstances))]
    private int _runningInstanceCount;

    [ObservableProperty] private double _totalRamMb;
    [ObservableProperty] private double _totalCpuPercent;
    [ObservableProperty] private int _intervalSeconds;

    /// <summary>True when at least one Claude process is running. Drives
    /// the "no instances" empty-state UI on the main window.</summary>
    public bool HasRunningInstances => RunningInstanceCount > 0;

    public MainWindowResourceViewModel(
        IResourceMonitor resourceMonitor,
        IWindowThumbnailService thumbnailService,
        SlotInstanceListViewModel slotInstances,
        ExternalInstanceListViewModel externalInstances,
        int initialIntervalSeconds,
        Func<bool> getThumbnailsEnabled,
        Action onIntervalChanged)
    {
        _resourceMonitor = resourceMonitor;
        _thumbnailService = thumbnailService;
        _slotInstances = slotInstances;
        _externalInstances = externalInstances;
        _getThumbnailsEnabled = getThumbnailsEnabled;
        _onIntervalChanged = onIntervalChanged;
        _intervalSeconds = initialIntervalSeconds;
        _runningInstanceCount = 0;

        _timer = new DispatcherTimer
            { Interval = TimeSpan.FromSeconds(_intervalSeconds) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
    }

    /// <summary>
    /// One refresh cycle: re-count running instances, pull the latest
    /// RAM/CPU snapshots from the monitor, push those into both list
    /// VMs, and (if the thumbnails setting is on) capture a fresh
    /// frame for every visible row. Safe to call on-demand from
    /// per-row actions as well as on the timer tick.
    /// </summary>
    public void Refresh()
    {
        var snapshots = _resourceMonitor.GetSnapshots();
        TotalRamMb = _resourceMonitor.TotalRamMb;
        TotalCpuPercent = _resourceMonitor.TotalCpuPercent;
        _slotInstances.Refresh(snapshots);
        _externalInstances.Refresh(snapshots);
        // Count after reconciliation so tray-resident slots (which have
        // no resource snapshot) are included in the running total.
        RunningInstanceCount = _slotInstances.Items.Count
            + _slotInstances.TrayItems.Count
            + _externalInstances.Items.Count;

        if (_getThumbnailsEnabled())
        {
            ThumbnailRefresher.RefreshVisibleThumbnails(
                _thumbnailService, _slotInstances.Items, 240, 150);
            ThumbnailRefresher.RefreshVisibleThumbnails(
                _thumbnailService, _externalInstances.Items, 240, 150);
        }
    }

    partial void OnIntervalSecondsChanged(int value)
    {
        if (value < 1) return;
        _timer.Interval = TimeSpan.FromSeconds(value);
        _onIntervalChanged();
    }

    /// <summary>
    /// Command wrapper around <see cref="Refresh"/> so the "?"
    /// refresh button in <c>ResourceTotalsRow.axaml</c> can bind
    /// to <c>Resources.ManualRefreshCommand</c>. Separate from
    /// <see cref="Refresh"/> itself because that stays a plain
    /// method usable as the callback passed to
    /// <c>SlotCallbackBinder.Bind</c>.
    /// </summary>
    [RelayCommand]
    private void ManualRefresh() => Refresh();
}
