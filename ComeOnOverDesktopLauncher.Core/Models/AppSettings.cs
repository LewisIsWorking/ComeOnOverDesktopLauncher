namespace ComeOnOverDesktopLauncher.Core.Models;

/// <summary>
/// User preferences persisted to disk between sessions.
/// </summary>
public class AppSettings
{
    public int DefaultSlotCount { get; set; } = 3;
    public string ComeOnOverUrl { get; set; } = "https://comeonover.netlify.app";
    public bool LaunchOnStartup { get; set; } = false;
    public int ResourceRefreshIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// User-defined names for each slot. Key = slot number, value = display name.
    /// Falls back to "Instance N" when no name is set.
    /// </summary>
    public Dictionary<int, string> SlotNames { get; set; } = new();

    public string GetSlotName(int slotNumber) =>
        SlotNames.TryGetValue(slotNumber, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : $"Instance {slotNumber}";
}
