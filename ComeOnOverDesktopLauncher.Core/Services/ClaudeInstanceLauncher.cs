using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.Core.Services;

/// <summary>
/// Launches and terminates Claude Desktop instances.
/// Uses unique --user-data-dir per slot so login sessions persist between launches.
/// Every launch attempt is logged with the resolved path, data dir and outcome
/// so that silent failures can be diagnosed from the log file.
/// </summary>
public class ClaudeInstanceLauncher : IClaudeInstanceLauncher
{
    private readonly IClaudePathResolver _pathResolver;
    private readonly IProcessService _processService;
    private readonly ILoggingService _logger;

    public ClaudeInstanceLauncher(
        IClaudePathResolver pathResolver,
        IProcessService processService,
        ILoggingService logger)
    {
        _pathResolver = pathResolver;
        _processService = processService;
        _logger = logger;
    }

    public void LaunchSlot(LaunchSlot slot)
    {
        _logger.LogInfo($"LaunchSlot called for slot {slot.SlotNumber}");

        var exePath = _pathResolver.ResolveClaudeExePath();
        if (exePath is null)
        {
            _logger.LogError("Claude executable not found - aborting launch");
            throw new InvalidOperationException("Claude executable not found. Is Claude Desktop installed?");
        }

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            slot.DataDirectoryName);
        var args = $"--user-data-dir=\"{dataDir}\"";

        _logger.LogInfo($"Starting process: {exePath} {args}");
        try
        {
            _processService.Start(exePath, args);
            _logger.LogInfo($"Process.Start completed for slot {slot.SlotNumber}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Process.Start threw for slot {slot.SlotNumber}", ex);
            throw;
        }
    }

    public void KillInstance(int processId)
    {
        _logger.LogInfo($"Killing process {processId}");
        _processService.KillProcess(processId);
    }

    /// <summary>
    /// Returns the number of Claude instances with a visible window.
    /// Uses windowed count to avoid counting Electron background child processes.
    /// </summary>
    public int GetRunningInstanceCount() =>
        _processService.CountByNameWithWindow("claude");
}
