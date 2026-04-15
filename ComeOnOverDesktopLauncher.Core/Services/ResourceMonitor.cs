using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.Core.Services;

/// <summary>
/// Polls Claude process resource usage. CPU % is computed as a delta
/// between the current poll and the previous one — call GetSnapshots()
/// on a regular interval (e.g. every 5s) for meaningful CPU readings.
/// First call always returns 0% CPU (no previous baseline to compare).
/// </summary>
public class ResourceMonitor : IResourceMonitor
{
    private readonly IProcessService _processService;
    private Dictionary<int, ProcessSnapshot> _previousSnapshots = new();

    public double TotalRamMb { get; private set; }
    public double TotalCpuPercent { get; private set; }

    public ResourceMonitor(IProcessService processService)
    {
        _processService = processService;
    }

    public IReadOnlyList<InstanceResourceSnapshot> GetSnapshots()
    {
        var current = _processService.GetWindowedProcessSnapshots("claude");
        var results = current
            .Select((snapshot, index) => BuildSnapshot(snapshot, index + 1))
            .ToList();

        _previousSnapshots = current.ToDictionary(s => s.ProcessId);

        TotalRamMb = Math.Round(results.Sum(s => s.RamMb), 1);
        TotalCpuPercent = Math.Round(results.Sum(s => s.CpuPercent), 1);

        return results;
    }

    private InstanceResourceSnapshot BuildSnapshot(ProcessSnapshot current, int instanceNumber)
    {
        var uptime = current.CapturedAt - current.StartTime;
        var cpuPercent = ComputeCpuPercent(current);

        return new InstanceResourceSnapshot(
            current.ProcessId,
            instanceNumber,
            cpuPercent,
            current.WorkingSetBytes,
            uptime);
    }

    private double ComputeCpuPercent(ProcessSnapshot current)
    {
        if (!_previousSnapshots.TryGetValue(current.ProcessId, out var previous))
            return 0.0;

        var cpuDelta = (current.TotalProcessorTime - previous.TotalProcessorTime).TotalMilliseconds;
        var timeDelta = (current.CapturedAt - previous.CapturedAt).TotalMilliseconds;

        if (timeDelta <= 0) return 0.0;

        var percent = cpuDelta / (timeDelta * Environment.ProcessorCount) * 100.0;
        return Math.Round(Math.Max(0, Math.Min(100, percent)), 1);
    }
}
