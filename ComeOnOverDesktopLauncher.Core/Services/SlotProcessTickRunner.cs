using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.Core.Services;

/// <summary>
/// Pure state machine that, given a stream of
/// <see cref="IProcessService.GetSlotProcesses"/> snapshots, emits a
/// <see cref="LaunchSlot"/> for each slot that was seen in a previous tick
/// but is absent from the current tick.
///
/// Separated from <see cref="SlotProcessMonitor"/> so that unit tests can
/// drive the tick logic directly without needing a real timer.
/// </summary>
public class SlotProcessTickRunner
{
    private readonly IProcessService _processService;
    private HashSet<int> _previouslySeenSlots = new();

    public SlotProcessTickRunner(IProcessService processService)
    {
        _processService = processService;
    }

    public IReadOnlyList<LaunchSlot> Tick()
    {
        var currentSlots = _processService.GetSlotProcesses()
            .Select(info => info.SlotNumber)
            .ToHashSet();

        var closed = _previouslySeenSlots
            .Where(n => !currentSlots.Contains(n))
            .Select(n => new LaunchSlot(n))
            .ToList();

        _previouslySeenSlots = currentSlots;
        return closed;
    }
}