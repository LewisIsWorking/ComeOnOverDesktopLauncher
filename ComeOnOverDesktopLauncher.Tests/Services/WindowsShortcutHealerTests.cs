using ComeOnOverDesktopLauncher.Core.Services.Interfaces;
using ComeOnOverDesktopLauncher.Services;
using ComeOnOverDesktopLauncher.Services.Interfaces;
using NSubstitute;
using System.Runtime.Versioning;

namespace ComeOnOverDesktopLauncher.Tests.Services;

/// <summary>
/// Exercises <see cref="WindowsShortcutHealer"/>'s branching logic
/// without touching real COM or the real filesystem. The healer's
/// two-dependency seam (running-exe probe + file-existence probe)
/// lets tests drive every <see cref="ShortcutHealResult"/> deterministically.
///
/// <para>
/// Annotated <see cref="SupportedOSPlatformAttribute"/> because
/// <see cref="WindowsShortcutHealer"/> is Windows-only. The whole
/// test assembly effectively is too (it tests a Windows-only app),
/// but we apply the attribute per-class so individual test files
/// don't need assembly-level coordination.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsShortcutHealerTests
{
    private readonly IShellLinkWriter _writer = Substitute.For<IShellLinkWriter>();
    private readonly ILoggingService _logger = Substitute.For<ILoggingService>();

    private static string ExpectedInstalledExe =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ComeOnOverDesktopLauncher", "current", "ComeOnOverDesktopLauncher.exe");

    private static string ExpectedShortcutPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Start Menu", "Programs",
            "ComeOnOverDesktopLauncher", "ComeOnOver Desktop Launcher.lnk");

    [Fact]
    public void HealIfMissing_RunningFromDevBuild_SkipsWithoutTouchingDisk()
    {
        var sut = new WindowsShortcutHealer(
            _writer, _logger,
            getRunningExePath: () => @"C:\code\CoODL\bin\Debug\net10.0\ComeOnOverDesktopLauncher.exe",
            fileExists: _ => throw new InvalidOperationException(
                "file-exists probe should not be invoked on dev build"));

        var result = sut.HealIfMissing();

        Assert.Equal(ShortcutHealResult.SkippedDevBuild, result);
        _writer.DidNotReceiveWithAnyArgs().TryCreateShortcut(default!, default!, default!);
    }

    [Fact]
    public void HealIfMissing_ShortcutPresent_ReportsAlreadyPresentAndDoesNotWrite()
    {
        var sut = new WindowsShortcutHealer(
            _writer, _logger,
            getRunningExePath: () => ExpectedInstalledExe,
            fileExists: path => path == ExpectedShortcutPath);

        var result = sut.HealIfMissing();

        Assert.Equal(ShortcutHealResult.AlreadyPresent, result);
        _writer.DidNotReceiveWithAnyArgs().TryCreateShortcut(default!, default!, default!);
    }

    [Fact]
    public void HealIfMissing_ShortcutMissingAndWriterSucceeds_HealsMissing()
    {
        _writer.TryCreateShortcut(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);
        var sut = new WindowsShortcutHealer(
            _writer, _logger,
            getRunningExePath: () => ExpectedInstalledExe,
            fileExists: _ => false);

        var result = sut.HealIfMissing();

        Assert.Equal(ShortcutHealResult.HealedMissing, result);
        _writer.Received(1).TryCreateShortcut(
            ExpectedShortcutPath,
            ExpectedInstalledExe,
            "ComeOnOver Desktop Launcher");
    }

    [Fact]
    public void HealIfMissing_ShortcutMissingAndWriterFails_ReturnsFailed()
    {
        _writer.TryCreateShortcut(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);
        var sut = new WindowsShortcutHealer(
            _writer, _logger,
            getRunningExePath: () => ExpectedInstalledExe,
            fileExists: _ => false);

        var result = sut.HealIfMissing();

        Assert.Equal(ShortcutHealResult.Failed, result);
    }

    [Fact]
    public void HealIfMissing_PathComparisonIsCaseInsensitive()
    {
        // Velopack records install paths case-preserving but Windows
        // filesystems are case-insensitive. Healer must not false-
        // negative on case-only drift.
        var sut = new WindowsShortcutHealer(
            _writer, _logger,
            getRunningExePath: () => ExpectedInstalledExe.ToUpperInvariant(),
            fileExists: _ => true);

        var result = sut.HealIfMissing();

        Assert.Equal(ShortcutHealResult.AlreadyPresent, result);
    }

    [Fact]
    public void HealIfMissing_RunningExeProbeThrows_CollapsesToFailedNotPropagated()
    {
        var sut = new WindowsShortcutHealer(
            _writer, _logger,
            getRunningExePath: () => throw new InvalidOperationException("probe failed"),
            fileExists: _ => true);

        var result = sut.HealIfMissing();

        Assert.Equal(ShortcutHealResult.Failed, result);
    }
}
