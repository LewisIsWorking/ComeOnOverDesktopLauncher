using ComeOnOverDesktopLauncher.Core.Models;

namespace ComeOnOverDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Manages Claude Desktop instance slots.
/// </summary>
public interface ISlotManager
{
    IReadOnlyList<LaunchSlot> GetSlots(int count);
    LaunchSlot GetNextAvailableSlot();
}
