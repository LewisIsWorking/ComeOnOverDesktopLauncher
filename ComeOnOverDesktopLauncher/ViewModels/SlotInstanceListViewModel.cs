using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.ViewModels;

/// <summary>
/// Owns the list of launcher-managed Claude slots - Claude processes
/// whose command line carries <c>--user-data-dir=...\ClaudeSlotN</c>.
///
/// <para>
/// Slots are split across two collections based on window visibility:
/// <see cref="Items"/> holds visible (windowed) slot rows rendered in
/// the main per-slot list, and <see cref="TrayItems"/> holds tray-
/// resident (close-to-tray'd) slot rows rendered in a separate
/// "Hidden / tray" section. Every launcher-managed slot appears in
/// exactly one of the two, never both.
/// </para>
///
/// <para>
/// The <c>IsTrayResident</c> bit comes from
/// <see cref="IClaudeProcessClassifier.TryClassifyAsSlot"/>, which
/// derives it from <c>ClaudeProcessInfo.IsWindowed</c> set by the
/// scanner. The view layer does no classification of its own.
/// </para>
///
/// <para>
/// Each snapshot's <c>InstanceNumber</c> is rewritten to the real
/// slot number extracted from its command line, so slot 3 renders as
/// "Slot 3" even when slots 1 and 2 are closed - the previous
/// sequential-enumeration approach mis-labelled this case.
/// </para>
/// </summary>
public partial class SlotInstanceListViewModel : ObservableObject
{
    private readonly IClaudeProcessScanner _scanner;
    private readonly IClaudeProcessClassifier _classifier;
    private readonly ISlotInitialiser _slotInitialiser;
    private readonly ILoggingService _logger;

    /// <summary>Visible (windowed) slot rows. Mutated in-place during
    /// <see cref="Refresh"/> to preserve Avalonia binding identity.</summary>
    public ObservableCollection<ClaudeInstanceViewModel> Items { get; } = new();

    /// <summary>Tray-resident (close-to-tray'd) slot rows. Mutated
    /// in-place during <see cref="Refresh"/>.</summary>
    public ObservableCollection<ClaudeInstanceViewModel> TrayItems { get; } = new();

    /// <summary>True when any slot is currently close-to-tray'd. Drives
    /// the IsVisible of the "Hidden / tray" section so it only appears
    /// when there's something to show.</summary>
    public bool HasTrayItems => TrayItems.Count > 0;

    public Func<int, string>? GetSlotName { get; set; }
    public Action<int, string>? OnSlotNameChanged { get; set; }
    public Action<int>? OnKillInstance { get; set; }

    public SlotInstanceListViewModel(
        IClaudeProcessScanner scanner,
        IClaudeProcessClassifier classifier,
        ISlotInitialiser slotInitialiser,
        ILoggingService logger)
    {
        _scanner = scanner;
        _classifier = classifier;
        _slotInitialiser = slotInitialiser;
        _logger = logger;
        TrayItems.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasTrayItems));
    }

    /// <summary>
    /// Rescans running Claude processes, filters to launcher-managed
    /// slots, and reconciles both <see cref="Items"/> and
    /// <see cref="TrayItems"/> in-place. Swallows scan exceptions and
    /// logs a warning rather than propagating: a transient WMI hiccup
    /// should not take down the poll tick. Previous state is preserved
    /// in that case.
    /// </summary>
    public void Refresh(IReadOnlyList<InstanceResourceSnapshot> resourceSnapshots)
    {
        try
        {
            var slotByPid = BuildSlotMap();
            var filtered = FilterAndRelabel(resourceSnapshots, slotByPid);
            ReconcileCollection(Items, filtered.Where(s => !s.IsTrayResident).ToList());
            ReconcileCollection(TrayItems, filtered.Where(s => s.IsTrayResident).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Slot instance refresh failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Scans all main Claude processes (windowed or tray-resident) and
    /// returns a <c>PID -&gt; (slotNumber, isTrayResident)</c> map for
    /// those that classify as slots. Non-slot processes are absent.
    /// </summary>
    private IReadOnlyDictionary<int, (int SlotNumber, bool IsTrayResident)> BuildSlotMap()
    {
        return _scanner.Scan()
            .Select(p => (p.ProcessId, Slot: _classifier.TryClassifyAsSlot(p)))
            .Where(x => x.Slot is not null)
            .ToDictionary(
                x => x.ProcessId,
                x => (x.Slot!.SlotNumber, x.Slot!.IsTrayResident));
    }

    /// <summary>
    /// Keeps only snapshots whose PID maps to a known slot, rewrites
    /// <c>InstanceNumber</c> to the real slot number, propagates the
    /// <c>IsTrayResident</c> bit from the classifier, and sorts by slot
    /// number so both collections render 1, 3, 5… in order.
    /// </summary>
    private static IReadOnlyList<InstanceResourceSnapshot> FilterAndRelabel(
        IReadOnlyList<InstanceResourceSnapshot> snapshots,
        IReadOnlyDictionary<int, (int SlotNumber, bool IsTrayResident)> slotByPid)
    {
        return snapshots
            .Where(s => slotByPid.ContainsKey(s.ProcessId))
            .Select(s =>
            {
                var (num, tray) = slotByPid[s.ProcessId];
                return s with { InstanceNumber = num, IsTrayResident = tray };
            })
            .OrderBy(s => s.InstanceNumber)
            .ToList();
    }

    /// <summary>
    /// In-place reconciliation of one target collection against a
    /// filtered snapshot list. Removes rows whose slot numbers are
    /// gone (handles middle-slot disappearance correctly), adds rows
    /// for newly-seen slot numbers, and updates resource fields on
    /// everyone. Preserves object identity for existing rows so
    /// binding state (edit-in-progress name text, etc.) survives.
    /// Used for both <see cref="Items"/> and <see cref="TrayItems"/>.
    /// </summary>
    private void ReconcileCollection(
        ObservableCollection<ClaudeInstanceViewModel> target,
        IReadOnlyList<InstanceResourceSnapshot> filtered)
    {
        var wantedSlots = filtered.Select(s => s.InstanceNumber).ToHashSet();

        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!wantedSlots.Contains(target[i].InstanceNumber))
                target.RemoveAt(i);
        }

        foreach (var snap in filtered)
        {
            var row = target.FirstOrDefault(vm => vm.InstanceNumber == snap.InstanceNumber);
            if (row is null)
            {
                var num = snap.InstanceNumber;
                var name = GetSlotName?.Invoke(num) ?? $"Instance {num}";
                var isSeeded = _slotInitialiser.IsSeeded(new LaunchSlot(num));
                row = new ClaudeInstanceViewModel(
                    num, name, isSeeded,
                    (n, v) => OnSlotNameChanged?.Invoke(n, v),
                    p => OnKillInstance?.Invoke(p));
                target.Add(row);
            }
            row.UpdateFrom(snap);
        }
    }
}
