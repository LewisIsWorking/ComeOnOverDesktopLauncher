using ComeOnOverDesktopLauncher.Core.Models;

namespace ComeOnOverDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Abstracts process operations to allow unit testing without spawning real processes.
/// </summary>
public interface IProcessService
{
    void Start(string fileName, string? arguments = null, bool useShellExecute = false);

    /// <summary>
    /// Starts a process with its stderr stream redirected to
    /// <paramref name="onStderrLine"/>, invoked once per line as the child
    /// process writes. Used specifically for Claude Desktop launches so
    /// upstream Electron/Node warnings (DEP0040 punycode, DEP0169
    /// url.parse, BuddyBleTransport handler errors,
    /// MaxListenersExceededWarning, etc.) are captured into CoODL's
    /// diagnostic log with a clear attribution tag rather than being
    /// lost (production - no console attached) or cluttering the dev
    /// terminal.
    ///
    /// The callback is invoked from a background task, so
    /// implementations must expect concurrent invocations if multiple
    /// processes are launched via this method. The reader task exits
    /// when the child process's stderr stream closes (typically at
    /// process exit); implementations must not leak threads across
    /// many launches.
    ///
    /// <b>Not for generic use.</b> Prefer <see cref="Start"/> for
    /// non-Claude launches (ComeOnOver web app, etc.) where we don't
    /// need stderr capture and want to keep stdio inheritance for
    /// easy debugging from a terminal.
    ///
    /// Returns the child process ID for log correlation, or 0 if the
    /// process failed to start.
    /// </summary>
    int StartWithStderrPipe(
        string fileName,
        string? arguments,
        Action<string> onStderrLine);
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
    /// Terminates the process with the given ID, including all its child processes.
    /// </summary>
    void KillProcess(int processId);
}