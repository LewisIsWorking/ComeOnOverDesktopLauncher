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
    /// When true, the launcher captures live thumbnails of each running
    /// Claude window on every poll tick and displays them alongside the
    /// slot's stats. On-by-default so the feature is discoverable;
    /// users who prefer a minimal launcher or have privacy concerns can
    /// toggle it off via the "Show thumbnails" checkbox. Thumbnails are
    /// kept in memory only (never written to disk).
    /// </summary>
    public bool ThumbnailsEnabled { get; set; } = true;

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
