using System.Runtime.Versioning;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;
using ComeOnOverDesktopLauncher.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.Services;

/// <summary>
/// Self-heals the Start Menu shortcut on startup after the v1.10.0 ->
/// v1.10.1 Velopack bug that occasionally leaves the Programs folder
/// empty post-update. See <c>docs/dev/VELOPACK.md</c> for the full
/// incident writeup.
///
/// <para>
/// Flow: determine the expected install path and expected shortcut
/// path; compare against what's on disk. On dev builds (exe living
/// outside <c>%LOCALAPPDATA%\ComeOnOverDesktopLauncher\current\</c>)
/// skip entirely. When installed-and-missing, recreate via
/// <see cref="IShellLinkWriter"/>. Never throws.
/// </para>
///
/// <para>
/// The "does the running exe live in the install dir?" check is
/// cheaper and more reliable than asking Velopack directly -
/// <c>UpdateManager.IsInstalled</c> depends on Velopack's own file
/// probes that we're trying to work around.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsShortcutHealer : IShortcutHealer
{
    private const string ExpectedInstallSubdir = @"ComeOnOverDesktopLauncher\current";
    private const string ExpectedExeName = "ComeOnOverDesktopLauncher.exe";
    private const string ShortcutDescription = "ComeOnOver Desktop Launcher";
    private const string StartMenuSubdir =
        @"Microsoft\Windows\Start Menu\Programs\ComeOnOverDesktopLauncher";
    private const string ShortcutFileName = "ComeOnOver Desktop Launcher.lnk";

    private readonly IShellLinkWriter _writer;
    private readonly ILoggingService _logger;
    private readonly Func<string> _getRunningExePath;
    private readonly Func<string, bool> _fileExists;

    /// <summary>Default constructor for DI - wires in the real
    /// running-exe lookup and <see cref="File.Exists"/>.</summary>
    public WindowsShortcutHealer(IShellLinkWriter writer, ILoggingService logger)
        : this(writer, logger, ResolveRunningExePath, File.Exists) { }

    /// <summary>Testing-seam constructor - lets unit tests substitute
    /// a fixed "running exe path" and a mock file-existence probe so
    /// the branching logic can be exercised without real filesystem
    /// state or matching Velopack install layout.</summary>
    public WindowsShortcutHealer(
        IShellLinkWriter writer,
        ILoggingService logger,
        Func<string> getRunningExePath,
        Func<string, bool> fileExists)
    {
        _writer = writer;
        _logger = logger;
        _getRunningExePath = getRunningExePath;
        _fileExists = fileExists;
    }

    public ShortcutHealResult HealIfMissing()
    {
        try
        {
            var runningExe = _getRunningExePath();
            var expectedInstallDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ExpectedInstallSubdir);
            var expectedExe = Path.Combine(expectedInstallDir, ExpectedExeName);

            if (!PathsEqual(runningExe, expectedExe))
            {
                _logger.LogInfo(
                    $"Shortcut heal skipped: running from '{runningExe}', " +
                    $"not Velopack install path '{expectedExe}' (dev build?)");
                return ShortcutHealResult.SkippedDevBuild;
            }

            var lnkPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                StartMenuSubdir, ShortcutFileName);

            if (_fileExists(lnkPath))
            {
                _logger.LogInfo($"Shortcut heal: already present at '{lnkPath}'");
                return ShortcutHealResult.AlreadyPresent;
            }

            _logger.LogWarning(
                $"Shortcut heal: missing at '{lnkPath}' - recreating " +
                $"(see docs/dev/VELOPACK.md for Velopack bug context)");
            var ok = _writer.TryCreateShortcut(lnkPath, expectedExe, ShortcutDescription);
            return ok ? ShortcutHealResult.HealedMissing : ShortcutHealResult.Failed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Shortcut heal crashed unexpectedly: {ex.Message}");
            return ShortcutHealResult.Failed;
        }
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(
            Path.GetFullPath(a).TrimEnd('\\'),
            Path.GetFullPath(b).TrimEnd('\\'),
            StringComparison.OrdinalIgnoreCase);

    private static string ResolveRunningExePath() =>
        Environment.ProcessPath ?? string.Empty;
}
