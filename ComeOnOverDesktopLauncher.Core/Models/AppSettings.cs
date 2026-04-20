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
    /// When true, the launcher's poll tick periodically checks for a
    /// new release via <see cref="Services.Interfaces.IAutoUpdateService"/>
    /// and silently downloads any available update in the background.
    /// On-by-default because auto-update is the whole point of the
    /// v1.10.0 Velopack switch; the user's only opt-out is this
    /// checkbox (exposed in <c>LaunchControlsPanel</c>). Persisted
    /// because users who disable it expect that choice to stick across
    /// restarts - losing the preference is worse than having it.
    ///
    /// <para>
    /// Note: this setting only controls the check-and-download phase.
    /// Applying the update (the actual restart) is always user-driven
    /// via the "Restart to install" button in the update banner, per
    /// the v1.10.0 UX spec (decision 4, option b).
    /// </para>
    /// </summary>
    public bool AutoCheckForUpdates { get; set; } = true;

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
