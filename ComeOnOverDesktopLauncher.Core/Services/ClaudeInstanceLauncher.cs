using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.Core.Services;

/// <summary>
/// Launches and terminates Claude Desktop instances.
/// Uses unique --user-data-dir per slot so login sessions persist between launches.
/// </summary>
public class ClaudeInstanceLauncher : IClaudeInstanceLauncher
{
    private readonly IClaudePathResolver _pathResolver;
    private readonly IProcessService _processService;

    public ClaudeInstanceLauncher(IClaudePathResolver pathResolver, IProcessService processService)
    {
        _pathResolver = pathResolver;
        _processService = processService;
    }

    public void LaunchSlot(LaunchSlot slot)
    {
        var exePath = _pathResolver.ResolveClaudeExePath()
            ?? throw new InvalidOperationException("Claude executable not found. Is Claude Desktop installed?");

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            slot.DataDirectoryName);

        _processService.Start(exePath, $"--user-data-dir=\"{dataDir}\"");
    }

    public void KillInstance(int processId) =>
        _processService.KillProcess(processId);

    /// <summary>
    /// Returns the number of Claude instances with a visible window.
    /// Uses windowed count to avoid counting Electron background child processes.
    /// </summary>
    public int GetRunningInstanceCount() =>
        _processService.CountByNameWithWindow("claude");
}
