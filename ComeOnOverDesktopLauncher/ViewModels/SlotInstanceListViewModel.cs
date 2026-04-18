using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.ViewModels;

/// <summary>
/// Owns the list of launcher-managed Claude slots - Claude processes
/// whose command line carries <c>--user-data-dir=...\ClaudeSlotN</c>.
///
/// Classification is done by running the windowed-Claude scan through
/// <see cref="IClaudeProcessClassifier.TryClassifyAsSlot"/> on each poll
/// tick. Snapshots whose PID doesn't classify as a slot (default-profile
/// Claude, Claude launched by some other tool) are dropped here and
/// surface instead in <see cref="ExternalInstanceListViewModel"/>, so
/// every windowed Claude appears in exactly one of the two lists.
///
/// The filter also relabels each snapshot's <c>InstanceNumber</c> with
/// the real slot number extracted from its command line, so slot 3 is
/// rendered as "Instance 3" even when slots 1 and 2 are closed - the
/// previous sequential-enumeration approach mis-labelled this case.
///
/// Per-row kill and rename callbacks are supplied by the owner
/// (<see cref="MainWindowViewModel"/>) after construction because they
/// need access to the launcher and to the shared mutable
/// <c>AppSettings</c>. Nullable so this VM is usable in tests that don't
/// exercise those flows.
/// </summary>
public partial class SlotInstanceListViewModel : ObservableObject
{
    private readonly IClaudeProcessScanner _scanner;
    private readonly IClaudeProcessClassifier _classifier;
    private readonly ISlotInitialiser _slotInitialiser;
    private readonly ILoggingService _logger;

    /// <summary>Bindable collection of per-slot VMs. Mutated in-place
    /// during <see cref="Refresh"/> so Avalonia bindings are preserved
    /// and the UI doesn't flicker.</summary>
    public ObservableCollection<ClaudeInstanceViewModel> Items { get; } = new();

    /// <summary>Resolves the user-defined name for a given slot number
    /// (falls back to "Instance N" when unset).</summary>
    public Func<int, string>? GetSlotName { get; set; }

    /// <summary>Invoked when the user edits a slot's display name.</summary>
    public Action<int, string>? OnSlotNameChanged { get; set; }

    /// <summary>Invoked when the user clicks a slot's kill button.</summary>
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
    }

    /// <summary>
    /// Rescans running Claude processes, filters to launcher-managed
    /// slots, and reconciles the <see cref="Items"/> collection
    /// in-place. Swallows scan exceptions and logs a warning rather than
    /// propagating: a transient WMI hiccup should not take down the poll
    /// tick. Previous state is preserved in that case.
    /// </summary>
    public void Refresh(IReadOnlyList<InstanceResourceSnapshot> resourceSnapshots)
    {
        try
        {
            var slotByPid = BuildSlotMap();
            var filtered = FilterAndRelabel(resourceSnapshots, slotByPid);
            Reconcile(filtered);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Slot instance refresh failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Scans all windowed Claude processes and returns a
    /// <c>PID -&gt; slotNumber</c> map for those that classify as slots.
    /// Non-slot processes are simply absent from the map.
    /// </summary>
    private IReadOnlyDictionary<int, int> BuildSlotMap()
    {
        return _scanner.Scan()
            .Select(p => (p.ProcessId, Slot: _classifier.TryClassifyAsSlot(p)))
            .Where(x => x.Slot is not null)
            .ToDictionary(x => x.ProcessId, x => x.Slot!.SlotNumber);
    }

    /// <summary>
    /// Keeps only snapshots whose PID maps to a known slot, rewrites
    /// each snapshot's <c>InstanceNumber</c> to the real slot number
    /// (not the sequential enumeration index), and sorts by slot number
    /// so the list always renders 1, 3, 5… in order even when gaps
    /// exist.
    /// </summary>
    private static IReadOnlyList<InstanceResourceSnapshot> FilterAndRelabel(
        IReadOnlyList<InstanceResourceSnapshot> snapshots,
        IReadOnlyDictionary<int, int> slotByPid)
    {
        return snapshots
            .Where(s => slotByPid.ContainsKey(s.ProcessId))
            .Select(s => s with { InstanceNumber = slotByPid[s.ProcessId] })
            .OrderBy(s => s.InstanceNumber)
            .ToList();
    }

    /// <summary>
    /// In-place reconciliation: remove rows whose slot numbers are no
    /// longer in the filtered set (handles middle-slot disappearance
    /// correctly - the previous remove-from-end logic was wrong), add
    /// rows for newly-seen slot numbers, and update resource fields for
    /// everyone. Preserves object identity for existing rows so binding
    /// state like edit-in-progress name text survives the refresh.
    /// </summary>
    private void Reconcile(IReadOnlyList<InstanceResourceSnapshot> filtered)
    {
        var wantedSlots = filtered.Select(s => s.InstanceNumber).ToHashSet();

        for (var i = Items.Count - 1; i >= 0; i--)
        {
            if (!wantedSlots.Contains(Items[i].InstanceNumber))
                Items.RemoveAt(i);
        }

        foreach (var snap in filtered)
        {
            var row = Items.FirstOrDefault(vm => vm.InstanceNumber == snap.InstanceNumber);
            if (row is null)
            {
                var num = snap.InstanceNumber;
                var name = GetSlotName?.Invoke(num) ?? $"Instance {num}";
                var isSeeded = _slotInitialiser.IsSeeded(new LaunchSlot(num));
                row = new ClaudeInstanceViewModel(
                    num, name, isSeeded,
                    (n, v) => OnSlotNameChanged?.Invoke(n, v),
                    p => OnKillInstance?.Invoke(p));
                Items.Add(row);
            }
            row.UpdateFrom(snap);
        }
    }
}
