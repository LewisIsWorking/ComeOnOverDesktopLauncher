using ComeOnOverDesktopLauncher.Core.Models;

namespace ComeOnOverDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Launches and terminates Claude Desktop instances.
/// </summary>
public interface IClaudeInstanceLauncher
{
    void LaunchSlot(LaunchSlot slot);
    int GetRunningInstanceCount();

    /// <summary>
    /// Terminates the Claude process with the given process ID.
    /// Used to fully close a Claude instance that would otherwise minimize to the tray.
    /// </summary>
    void KillInstance(int processId);
}
