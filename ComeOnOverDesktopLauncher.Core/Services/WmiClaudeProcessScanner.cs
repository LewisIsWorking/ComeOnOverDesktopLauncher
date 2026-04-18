using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.Core.Services;

/// <summary>
/// Windows-native <see cref="IClaudeProcessScanner"/> using WMI
/// (<c>SELECT ProcessId, ParentProcessId, CommandLine FROM Win32_Process
/// WHERE Name='claude.exe'</c>) to read command lines, parent linkage,
/// and process start times.
///
/// <b>Windowed-only filter:</b> Claude is an Electron app; each running
/// Claude window spawns ~10 child processes (renderer, GPU, crashpad
/// handler, audio/video/network utility services, node-service). To avoid
/// flooding the UI with a row per child process, the scanner drops any
/// PID whose <see cref="Process.MainWindowHandle"/> is zero. This matches
/// the established <see cref="IProcessService.GetWindowedProcessSnapshots"/>
/// convention.
///
/// <b>Command-line enrichment:</b> Chromium/Electron's "browser main"
/// process (the one with the visible window) reports an empty args list
/// to WMI - its <c>--user-data-dir</c> flag is only copied into child
/// processes during fork. Classification (slot vs external) needs that
/// flag, so the scanner walks each windowed main's direct children via
/// <c>ParentProcessId</c> and, when the main's own cmdline is missing
/// the flag, extracts <c>--user-data-dir=...</c> from any child and
/// appends it. The result is a clean, minimal cmdline that still carries
/// the one piece of information the classifier needs.
///
/// WMI is currently the only reliable way to read another process's
/// command line on Windows from managed code:
/// <see cref="Process.StartInfo.Arguments"/> is only populated for
/// processes we started ourselves, and the
/// <c>NtQueryInformationProcess</c> + PEB-read alternative requires
/// <c>SeDebugPrivilege</c>.
/// </summary>
[SupportedOSPlatform("windows")]
public class WmiClaudeProcessScanner : IClaudeProcessScanner
{
    private const string UserDataDirFlag = "--user-data-dir=";

    private readonly ILoggingService _logger;

    public WmiClaudeProcessScanner(ILoggingService logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<ClaudeProcessInfo> Scan()
    {
        var windowedPids = GetWindowedClaudePids();
        if (windowedPids.Count == 0) return Array.Empty<ClaudeProcessInfo>();

        var all = QueryAllClaudeProcesses();
        var childrenByParent = all
            .GroupBy(p => p.ParentPid)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ProcessRecord>)g.ToList());

        var results = new List<ClaudeProcessInfo>();
        foreach (var pid in windowedPids)
        {
            var self = all.FirstOrDefault(p => p.Pid == pid);
            if (self is null) continue; // raced with process exit

            var effectiveCmdline = EnrichWithUserDataDir(self, childrenByParent);
            results.Add(new ClaudeProcessInfo(pid, effectiveCmdline, TryGetStartTime(pid)));
        }

        return results;
    }

    private static HashSet<int> GetWindowedClaudePids()
    {
        var procs = Process.GetProcessesByName("claude");
        try
        {
            return procs
                .Where(p => p.MainWindowHandle != IntPtr.Zero)
                .Select(p => p.Id)
                .ToHashSet();
        }
        finally
        {
            foreach (var p in procs) p.Dispose();
        }
    }

    private static List<ProcessRecord> QueryAllClaudeProcesses()
    {
        var results = new List<ProcessRecord>();
        using var searcher = new ManagementObjectSearcher(
            "SELECT ProcessId, ParentProcessId, CommandLine FROM Win32_Process WHERE Name = 'claude.exe'");
        foreach (var obj in searcher.Get())
        {
            using var mo = obj;
            var pid = Convert.ToInt32(mo["ProcessId"]);
            var ppid = mo["ParentProcessId"] is null ? 0 : Convert.ToInt32(mo["ParentProcessId"]);
            var cmd = mo["CommandLine"] as string ?? string.Empty;
            results.Add(new ProcessRecord(pid, ppid, cmd));
        }
        return results;
    }

    /// <summary>
    /// Returns <paramref name="windowed"/>.CommandLine if it already carries
    /// a <c>--user-data-dir</c> flag; otherwise synthesises a minimal
    /// cmdline by appending the flag extracted from one of the main's
    /// direct children. If no child carries the flag either, the original
    /// cmdline is returned unchanged (classification will simply treat the
    /// process as external).
    /// </summary>
    private static string EnrichWithUserDataDir(
        ProcessRecord windowed,
        IReadOnlyDictionary<int, IReadOnlyList<ProcessRecord>> childrenByParent)
    {
        if (windowed.CommandLine.Contains(UserDataDirFlag, StringComparison.OrdinalIgnoreCase))
            return windowed.CommandLine;

        if (!childrenByParent.TryGetValue(windowed.Pid, out var children))
            return windowed.CommandLine;

        foreach (var child in children)
        {
            var value = ExtractFlagValue(child.CommandLine, UserDataDirFlag);
            if (value is null) continue;
            var own = windowed.CommandLine.TrimEnd();
            return $"{own} {UserDataDirFlag}{value}";
        }

        return windowed.CommandLine;
    }

    /// <summary>
    /// Extracts the value of a <c>--flag=value</c> argument from a command
    /// line. Handles quoted values (<c>--flag="path with space"</c>) and
    /// bare values (<c>--flag=C:\nospaces</c>). Returns <c>null</c> when
    /// the flag is absent.
    /// </summary>
    private static string? ExtractFlagValue(string commandLine, string flagPrefix)
    {
        var idx = commandLine.IndexOf(flagPrefix, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        var after = commandLine[(idx + flagPrefix.Length)..];
        if (after.StartsWith('"'))
        {
            var close = after.IndexOf('"', 1);
            return close > 0 ? after[1..close] : after[1..];
        }
        var space = after.IndexOf(' ');
        return space > 0 ? after[..space] : after;
    }

    private DateTime TryGetStartTime(int pid)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            return proc.StartTime;
        }
        catch (ArgumentException)
        {
            return DateTime.MinValue;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Could not read start time for PID {pid}: {ex.Message}");
            return DateTime.MinValue;
        }
    }

    private record ProcessRecord(int Pid, int ParentPid, string CommandLine);
}