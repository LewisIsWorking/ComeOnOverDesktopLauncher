namespace ComeOnOverDesktopLauncher.Core.Models;

/// <summary>
/// Links a running Claude process (by PID) to the launcher-managed slot number
/// it is using, determined by inspecting the process commandline for a
/// <c>--user-data-dir="...\ClaudeSlotN"</c> flag.
/// Used by <see cref="Services.Interfaces.ISlotManager"/> to decide which slot
/// numbers are currently occupied, and by the resource monitor to classify
/// processes as launcher-managed vs external.
/// </summary>
public record SlotProcessInfo(int ProcessId, int SlotNumber);