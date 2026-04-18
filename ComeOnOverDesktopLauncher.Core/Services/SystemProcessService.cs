using System.Diagnostics;
using System.Runtime.Versioning;
using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.Core.Services;

/// <summary>
/// Production <see cref="IProcessService"/> built on
/// <see cref="System.Diagnostics.Process"/>.
/// Slot/external classification of running Claude instances has moved to
/// <see cref="IClaudeProcessScanner"/> + <see cref="IClaudeProcessClassifier"/>
/// so that classification concerns stay out of the process-control service.
/// </summary>
[SupportedOSPlatform("windows")]
public class SystemProcessService : IProcessService
{
    public void Start(string fileName, string? arguments = null, bool useShellExecute = false)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments ?? string.Empty,
            UseShellExecute = useShellExecute
        });
    }

    public int CountByName(string processName) =>
        Process.GetProcessesByName(processName).Length;

    public int CountByNameWithWindow(string processName) =>
        Process.GetProcessesByName(processName)
            .Count(p => p.MainWindowHandle != IntPtr.Zero);

    public IReadOnlyList<ProcessSnapshot> GetWindowedProcessSnapshots(string processName)
    {
        var now = DateTime.UtcNow;
        return Process.GetProcessesByName(processName)
            .Where(p => p.MainWindowHandle != IntPtr.Zero)
            .Select(p => new ProcessSnapshot(
                p.Id,
                p.WorkingSet64,
                p.TotalProcessorTime,
                p.StartTime.ToUniversalTime(),
                now))
            .ToList();
    }

    public void KillProcess(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
        }
        catch (ArgumentException)
        {
            // Process already exited - nothing to do
        }
    }
}