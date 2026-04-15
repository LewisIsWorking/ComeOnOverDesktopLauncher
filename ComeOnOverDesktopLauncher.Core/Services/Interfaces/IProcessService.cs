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
}
