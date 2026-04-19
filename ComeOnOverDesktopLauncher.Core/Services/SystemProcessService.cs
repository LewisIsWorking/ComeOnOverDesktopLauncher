using System.Diagnostics;
using System.Runtime.Versioning;
using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.Core.Services;

/// <summary>
/// Production <see cref="IProcessService"/> built on
/// <see cref="System.Diagnostics.Process"/>.
///
/// <para>
/// <see cref="Start"/> performs a fire-and-forget launch without stream
/// redirection, used by <see cref="ComeOnOverAppService"/> to open URLs
/// via the default browser.
/// </para>
///
/// <para>
/// <see cref="StartWithStderrPipe"/> redirects the child's stderr and
/// pipes each line to the supplied callback, used for Claude slot
/// launches so upstream Electron/Node warnings (DEP0040, DEP0169,
/// BuddyBleTransport, MaxListeners, etc.) flow into CoODL's log
/// rather than being lost (prod, no console) or cluttering the dev
/// terminal. The callback is the caller's concern - this service
/// stays pure process control.
/// </para>
///
/// Slot/external classification of running Claude instances lives in
/// <see cref="IClaudeProcessScanner"/> + <see cref="IClaudeProcessClassifier"/>
/// so classification concerns stay out of the process-control service.
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

    public int StartWithStderrPipe(
        string fileName,
        string? arguments,
        Action<string> onStderrLine)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments ?? string.Empty,
            UseShellExecute = false,
            RedirectStandardError = true,
            // stdout is not redirected so we keep the child's default
            // stdio behaviour for stdout - Claude Desktop rarely prints
            // there anyway, and redirecting both can cause deadlocks if
            // one stream blocks while the reader is draining the other.
        };

        var process = Process.Start(psi);
        if (process is null) return 0;

        var pid = process.Id;

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                onStderrLine(e.Data);
        };
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => process.Dispose();

        process.BeginErrorReadLine();
        return pid;
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
