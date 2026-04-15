using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.Core.Services;

/// <summary>
/// Manages Claude Desktop instance slots.
/// Slots are fixed and named to preserve login sessions between launches.
/// </summary>
public class SlotManager : ISlotManager
{
    private readonly IProcessService _processService;

    public SlotManager(IProcessService processService)
    {
        _processService = processService;
    }

    public IReadOnlyList<LaunchSlot> GetSlots(int count)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be at least 1.");

        return Enumerable.Range(1, count)
            .Select(i => new LaunchSlot(i))
            .ToList();
    }

    /// <summary>
    /// Returns the next slot based on windowed instance count.
    /// Avoids counting Electron's many background child processes.
    /// </summary>
    public LaunchSlot GetNextAvailableSlot()
    {
        var runningCount = _processService.CountByNameWithWindow("claude");
        return new LaunchSlot(runningCount + 1);
    }
}
