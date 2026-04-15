using ComeOnOverDesktopLauncher.Core.Models;

namespace ComeOnOverDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Launches Claude Desktop instances into named persistent slots.
/// </summary>
public interface IClaudeInstanceLauncher
{
    void LaunchSlot(LaunchSlot slot);
    int GetRunningInstanceCount();
}
