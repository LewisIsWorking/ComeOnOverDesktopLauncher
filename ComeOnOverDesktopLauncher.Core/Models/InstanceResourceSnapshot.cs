namespace ComeOnOverDesktopLauncher.Core.Models;

/// <summary>
/// Computed resource usage for a single Claude instance.
/// CPU % is calculated as a delta between two polls.
/// </summary>
public record InstanceResourceSnapshot(
    int ProcessId,
    int InstanceNumber,
    double CpuPercent,
    long RamBytes,
    TimeSpan Uptime)
{
    public double RamMb => Math.Round(RamBytes / (1024.0 * 1024.0), 1);

    public string UptimeDisplay => Uptime.TotalHours >= 1
        ? $"{(int)Uptime.TotalHours}h {Uptime.Minutes}m"
        : $"{Uptime.Minutes}m {Uptime.Seconds}s";
}
