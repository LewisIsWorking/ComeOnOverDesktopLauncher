using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.Core.Services.Linux;

/// <summary>
/// Linux stub IClaudeProcessScanner: always returns empty. Real
/// implementation will parse /proc/*/cmdline and /proc/*/stat to find
/// running electron processes whose command line contains
/// --user-data-dir=$HOME/.config/ClaudeSlot* - that's the
/// ProcfsClaudeProcessScanner deferred to milestone 3.
///
/// <para>For the v1.10.19 build-and-run MVP, returning empty means
/// the launcher UI shows zero running instances even when Claude IS
/// running on Linux. The Launch button still works (the launcher
/// fires the binary), but the instance won't appear in the slot list
/// until the procfs scanner lands.</para>
/// </summary>
public class EmptyClaudeProcessScanner : IClaudeProcessScanner
{
    public IReadOnlyList<ClaudeProcessInfo> Scan() => Array.Empty<ClaudeProcessInfo>();
}
