using ComeOnOverDesktopLauncher.Core.Models;

namespace ComeOnOverDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Abstracts process operations to allow unit testing without spawning real processes.
/// </summary>
public interface IProcessService
{
    void Start(string fileName, string? arguments = null, bool useShellExecute = false);
    int CountByName(string processName);

    /// <summary>
    /// Counts only processes with a visible main window.
    /// Use this for Electron apps that spawn many background child processes.
    /// </summary>
    int CountByNameWithWindow(string processName);

    /// <summary>
    /// Returns raw snapshots for all windowed processes with the given name.
    /// Used by ResourceMonitor to compute CPU and RAM usage.
    /// </summary>
    IReadOnlyList<ProcessSnapshot> GetWindowedProcessSnapshots(string processName);

    /// <summary>
    /// Returns the PID and slot number of every running Claude process whose
    /// commandline contains <c>--user-data-dir=...\ClaudeSlotN</c>.
    /// Processes launched outside the launcher (default profile or other tools)
    /// are NOT returned here - use <see cref="GetWindowedProcessSnapshots"/>
    /// and cross-reference if classification is needed.
    /// </summary>
    IReadOnlyList<SlotProcessInfo> GetSlotProcesses();

    /// <summary>
    /// Terminates the process with the given ID, including all its child processes.
    /// </summary>
    void KillProcess(int processId);
}