using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;
using ComeOnOverDesktopLauncher.Services.Interfaces;
using ComeOnOverDesktopLauncher.ViewModels;
using NSubstitute;

namespace ComeOnOverDesktopLauncher.Tests.ViewModels;

public class ExternalInstanceListViewModelTests
{
    private readonly IClaudeProcessScanner _scanner = Substitute.For<IClaudeProcessScanner>();
    private readonly IClaudeProcessClassifier _classifier = Substitute.For<IClaudeProcessClassifier>();
    private readonly IConfirmDialogService _confirmDialog = Substitute.For<IConfirmDialogService>();
    private readonly IProcessService _processService = Substitute.For<IProcessService>();
    private readonly ILoggingService _logger = Substitute.For<ILoggingService>();

    private ExternalInstanceListViewModel CreateSut() =>
        new(_scanner, _classifier, _confirmDialog, _processService, _logger);

    private static ClaudeProcessInfo Claude(int pid, string cmdLine = "") =>
        new(pid, cmdLine, DateTime.UtcNow);

    private static ExternalProcessInfo External(int pid, string cmdLine = "claude.exe") =>
        new(pid, cmdLine, DateTime.UtcNow);

    private static InstanceResourceSnapshot Snap(
        int pid,
        double cpu = 0,
        double ramMb = 0,
        TimeSpan uptime = default) =>
        new(pid, 0, cpu, (long)(ramMb * 1024 * 1024), uptime);

    [Fact]
    public void Refresh_WhenScannerReturnsEmpty_ItemsStaysEmpty()
    {
        _scanner.Scan().Returns(Array.Empty<ClaudeProcessInfo>());
        var sut = CreateSut();

        sut.Refresh(Array.Empty<InstanceResourceSnapshot>());

        Assert.Empty(sut.Items);
        Assert.False(sut.HasExternalInstances);
    }

    [Fact]
    public void Refresh_AddsItemForEachExternalProcess()
    {
        var p1 = Claude(100); var p2 = Claude(200);
        _scanner.Scan().Returns(new[] { p1, p2 });
        _classifier.TryClassifyAsExternal(p1).Returns(External(100));
        _classifier.TryClassifyAsExternal(p2).Returns(External(200));
        var sut = CreateSut();

        sut.Refresh(Array.Empty<InstanceResourceSnapshot>());

        Assert.Equal(2, sut.Items.Count);
        Assert.Contains(sut.Items, i => i.Pid == 100);
        Assert.Contains(sut.Items, i => i.Pid == 200);
        Assert.True(sut.HasExternalInstances);
    }

    [Fact]
    public void Refresh_SkipsSlotProcesses()
    {
        var slotProc = Claude(100);
        var extProc = Claude(200);
        _scanner.Scan().Returns(new[] { slotProc, extProc });
        _classifier.TryClassifyAsExternal(slotProc).Returns((ExternalProcessInfo?)null);
        _classifier.TryClassifyAsExternal(extProc).Returns(External(200));
        var sut = CreateSut();

        sut.Refresh(Array.Empty<InstanceResourceSnapshot>());

        Assert.Single(sut.Items);
        Assert.Equal(200, sut.Items[0].Pid);
    }

    [Fact]
    public void Refresh_RemovesItemsWhosePidsAreGone()
    {
        var p1 = Claude(100);
        _scanner.Scan().Returns(new[] { p1 });
        _classifier.TryClassifyAsExternal(p1).Returns(External(100));
        var sut = CreateSut();
        sut.Refresh(Array.Empty<InstanceResourceSnapshot>());
        Assert.Single(sut.Items);

        // Second refresh: scanner returns empty
        _scanner.Scan().Returns(Array.Empty<ClaudeProcessInfo>());
        sut.Refresh(Array.Empty<InstanceResourceSnapshot>());

        Assert.Empty(sut.Items);
        Assert.False(sut.HasExternalInstances);
    }

    [Fact]
    public void Refresh_PreservesExistingItemIdentityAcrossRefreshes()
    {
        var p1 = Claude(100);
        _scanner.Scan().Returns(new[] { p1 });
        _classifier.TryClassifyAsExternal(p1).Returns(External(100));
        var sut = CreateSut();

        sut.Refresh(Array.Empty<InstanceResourceSnapshot>());
        var firstInstance = sut.Items[0];

        sut.Refresh(Array.Empty<InstanceResourceSnapshot>());
        var secondInstance = sut.Items[0];

        Assert.Same(firstInstance, secondInstance);
    }

    [Fact]
    public void Refresh_CorrelatesSnapshotsByPid_UpdatingExistingItems()
    {
        var p1 = Claude(100);
        _scanner.Scan().Returns(new[] { p1 });
        _classifier.TryClassifyAsExternal(p1).Returns(External(100));
        var sut = CreateSut();

        sut.Refresh(new[] { Snap(100, cpu: 12.5, ramMb: 300, uptime: TimeSpan.FromMinutes(5)) });

        Assert.Equal(12.5, sut.Items[0].CpuPercent);
        Assert.Equal(300.0, sut.Items[0].RamMb);
    }

    [Fact]
    public void Refresh_IgnoresSnapshotsWithoutMatchingPid()
    {
        var p1 = Claude(100);
        _scanner.Scan().Returns(new[] { p1 });
        _classifier.TryClassifyAsExternal(p1).Returns(External(100));
        var sut = CreateSut();

        // snapshot for different PID
        sut.Refresh(new[] { Snap(999, cpu: 50.0) });

        Assert.Equal(0, sut.Items[0].CpuPercent);
    }

    [Fact]
    public void Refresh_RecomputesTotalsFromItems()
    {
        var p1 = Claude(100); var p2 = Claude(200);
        _scanner.Scan().Returns(new[] { p1, p2 });
        _classifier.TryClassifyAsExternal(p1).Returns(External(100));
        _classifier.TryClassifyAsExternal(p2).Returns(External(200));
        var sut = CreateSut();

        sut.Refresh(new[]
        {
            Snap(100, cpu: 5.0, ramMb: 100),
            Snap(200, cpu: 7.5, ramMb: 200)
        });

        Assert.Equal(300.0, sut.TotalRamMb);
        Assert.Equal(12.5, sut.TotalCpuPercent);
    }

    [Fact]
    public void Refresh_WhenScannerThrows_PreservesPreviousStateAndLogsWarning()
    {
        var p1 = Claude(100);
        _scanner.Scan().Returns(new[] { p1 });
        _classifier.TryClassifyAsExternal(p1).Returns(External(100));
        var sut = CreateSut();
        sut.Refresh(Array.Empty<InstanceResourceSnapshot>());
        Assert.Single(sut.Items);

        _scanner.Scan().Returns<IReadOnlyList<ClaudeProcessInfo>>(_ => throw new InvalidOperationException("WMI dead"));
        sut.Refresh(Array.Empty<InstanceResourceSnapshot>());

        // Previous state preserved
        Assert.Single(sut.Items);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("WMI dead")), Arg.Any<string>());
    }

    [Fact]
    public async Task Close_WhenUserConfirms_KillsProcess()
    {
        var p1 = Claude(100);
        _scanner.Scan().Returns(new[] { p1 });
        _classifier.TryClassifyAsExternal(p1).Returns(External(100));
        _confirmDialog.ConfirmAsync(Arg.Any<ConfirmDialogOptions>()).Returns(true);
        var sut = CreateSut();
        sut.Refresh(Array.Empty<InstanceResourceSnapshot>());

        await sut.Items[0].CloseCommand.ExecuteAsync(null);

        _processService.Received(1).KillProcess(100);
    }

    [Fact]
    public async Task Close_WhenUserCancels_DoesNotKillProcess()
    {
        var p1 = Claude(100);
        _scanner.Scan().Returns(new[] { p1 });
        _classifier.TryClassifyAsExternal(p1).Returns(External(100));
        _confirmDialog.ConfirmAsync(Arg.Any<ConfirmDialogOptions>()).Returns(false);
        var sut = CreateSut();
        sut.Refresh(Array.Empty<InstanceResourceSnapshot>());

        await sut.Items[0].CloseCommand.ExecuteAsync(null);

        _processService.DidNotReceive().KillProcess(Arg.Any<int>());
    }

    [Fact]
    public async Task Close_ConfirmDialogIsDestructiveSeverity()
    {
        var p1 = Claude(100);
        _scanner.Scan().Returns(new[] { p1 });
        _classifier.TryClassifyAsExternal(p1).Returns(External(100));
        _confirmDialog.ConfirmAsync(Arg.Any<ConfirmDialogOptions>()).Returns(false);
        var sut = CreateSut();
        sut.Refresh(Array.Empty<InstanceResourceSnapshot>());

        await sut.Items[0].CloseCommand.ExecuteAsync(null);

        await _confirmDialog.Received(1).ConfirmAsync(
            Arg.Is<ConfirmDialogOptions>(o => o.Severity == DialogSeverity.Destructive));
    }

    [Fact]
    public async Task Close_WhenKillThrows_LogsErrorAndDoesNotRethrow()
    {
        var p1 = Claude(100);
        _scanner.Scan().Returns(new[] { p1 });
        _classifier.TryClassifyAsExternal(p1).Returns(External(100));
        _confirmDialog.ConfirmAsync(Arg.Any<ConfirmDialogOptions>()).Returns(true);
        _processService.When(s => s.KillProcess(100)).Do(_ => throw new UnauthorizedAccessException("denied"));
        var sut = CreateSut();
        sut.Refresh(Array.Empty<InstanceResourceSnapshot>());

        await sut.Items[0].CloseCommand.ExecuteAsync(null);

        _logger.Received(1).LogError(
            Arg.Is<string>(s => s.Contains("Failed to close") && s.Contains("100")),
            Arg.Any<Exception>(),
            Arg.Any<string>());
    }
}