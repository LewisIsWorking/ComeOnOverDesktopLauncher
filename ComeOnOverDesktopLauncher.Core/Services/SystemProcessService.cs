using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.Core.Services;

/// <summary>
/// Production process implementation using System.Diagnostics.Process.
/// Slot detection (GetSlotProcesses) uses WMI/CIM to read commandlines,
/// which is the only reliable way to map a PID to its --user-data-dir on Windows.
/// </summary>
[SupportedOSPlatform("windows")]
public class SystemProcessService : IProcessService
{
    private static readonly Regex SlotPattern = new(
        @"--user-data-dir=""?[^""]*\\ClaudeSlot(\d+)""?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

    public IReadOnlyList<SlotProcessInfo> GetSlotProcesses()
    {
        var results = new List<SlotProcessInfo>();
        using var searcher = new ManagementObjectSearcher(
            "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'claude.exe'");
        foreach (var obj in searcher.Get())
        {
            using var mo = obj;
            var commandLine = mo["CommandLine"] as string;
            if (string.IsNullOrEmpty(commandLine)) continue;

            var match = SlotPattern.Match(commandLine);
            if (!match.Success) continue;
            if (!int.TryParse(match.Groups[1].Value, out var slotNumber)) continue;

            var pid = Convert.ToInt32(mo["ProcessId"]);
            results.Add(new SlotProcessInfo(pid, slotNumber));
        }
        return results;
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