using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;
using ComeOnOverDesktopLauncher.ViewModels;
using NSubstitute;

namespace ComeOnOverDesktopLauncher.Tests.ViewModels;

public class SlotInstanceListViewModelTests
{
    private readonly IClaudeProcessScanner _scanner = Substitute.For<IClaudeProcessScanner>();
    private readonly IClaudeProcessClassifier _classifier = Substitute.For<IClaudeProcessClassifier>();
    private readonly ISlotInitialiser _slotInitialiser = Substitute.For<ISlotInitialiser>();
    private readonly ILoggingService _logger = Substitute.For<ILoggingService>();

    private SlotInstanceListViewModel CreateSut() =>
        new(_scanner, _classifier, _slotInitialiser, _logger);

    private static ClaudeProcessInfo Claude(int pid) =>
        new(pid, "", DateTime.UtcNow);

    private static InstanceResourceSnapshot Snap(
        int pid,
        int instanceNumber = 0,
        double cpu = 0,
        double ramMb = 0) =>
        new(pid, instanceNumber, cpu, (long)(ramMb * 1024 * 1024), TimeSpan.Zero);

    /// <summary>
    /// Configures the scanner to return the given claude procs and the
    /// classifier to map each one to a slot (or <c>null</c> for not-slot)
    /// based on the supplied mappings.
    /// </summary>
    private void ReturnSlots(params (int pid, int? slotNumber)[] mappings)
    {
        var procs = mappings.Select(m => Claude(m.pid)).ToArray();
        _scanner.Scan().Returns(procs);
        for (var i = 0; i < mappings.Length; i++)
        {
            var (pid, slot) = mappings[i];
            _classifier.TryClassifyAsSlot(procs[i])
                .Returns(slot.HasValue ? new SlotProcessInfo(pid, slot.Value) : null);
        }
    }

    [Fact]
    public void Refresh_WhenNoSnapshots_ItemsEmpty()
    {
        _scanner.Scan().Returns(Array.Empty<ClaudeProcessInfo>());
        var sut = CreateSut();

        sut.Refresh(Array.Empty<InstanceResourceSnapshot>());

        Assert.Empty(sut.Items);
    }

    [Fact]
    public void Refresh_AddsRowForEachSlotPid()
    {
        ReturnSlots((100, 1), (200, 2));
        var sut = CreateSut();

        sut.Refresh(new[] { Snap(100), Snap(200) });

        Assert.Equal(2, sut.Items.Count);
        Assert.Contains(sut.Items, i => i.InstanceNumber == 1);
        Assert.Contains(sut.Items, i => i.InstanceNumber == 2);
    }

    [Fact]
    public void Refresh_SkipsNonSlotPids()
    {
        ReturnSlots((100, 1), (200, null));
        var sut = CreateSut();

        sut.Refresh(new[] { Snap(100), Snap(200) });

        Assert.Single(sut.Items);
        Assert.Equal(1, sut.Items[0].InstanceNumber);
    }

    [Fact]
    public void Refresh_RelabelsInstanceNumberWithRealSlotNumber()
    {
        // Even if the snapshot came in with InstanceNumber=1 from
        // ResourceMonitor's sequential enumeration, the slot VM should
        // relabel to the real ClaudeSlotN number from the classifier.
        ReturnSlots((100, 3));
        var sut = CreateSut();

        sut.Refresh(new[] { Snap(100, instanceNumber: 1) });

        Assert.Single(sut.Items);
        Assert.Equal(3, sut.Items[0].InstanceNumber);
    }

    [Fact]
    public void Refresh_SortsBySlotNumber()
    {
        ReturnSlots((100, 3), (200, 1), (300, 2));
        var sut = CreateSut();

        sut.Refresh(new[] { Snap(100), Snap(200), Snap(300) });

        Assert.Equal(1, sut.Items[0].InstanceNumber);
        Assert.Equal(2, sut.Items[1].InstanceNumber);
        Assert.Equal(3, sut.Items[2].InstanceNumber);
    }

    [Fact]
    public void Refresh_PreservesRowIdentityAcrossRefreshes()
    {
        ReturnSlots((100, 1));
        var sut = CreateSut();
        sut.Refresh(new[] { Snap(100) });
        var first = sut.Items[0];

        sut.Refresh(new[] { Snap(100) });
        var second = sut.Items[0];

        Assert.Same(first, second);
    }

    [Fact]
    public void Refresh_UpdatesExistingRowResourceFields()
    {
        ReturnSlots((100, 1));
        var sut = CreateSut();
        sut.Refresh(new[] { Snap(100, cpu: 5.0, ramMb: 100) });

        ReturnSlots((100, 1));
        sut.Refresh(new[] { Snap(100, cpu: 15.0, ramMb: 250) });

        Assert.Single(sut.Items);
        Assert.Equal(15.0, sut.Items[0].CpuPercent);
        Assert.Equal(250.0, sut.Items[0].RamMb);
    }

    [Fact]
    public void Refresh_RemovesRowsWhoseSlotsAreGone()
    {
        ReturnSlots((100, 1), (200, 2));
        var sut = CreateSut();
        sut.Refresh(new[] { Snap(100), Snap(200) });

        ReturnSlots((200, 2));
        sut.Refresh(new[] { Snap(200) });

        Assert.Single(sut.Items);
        Assert.Equal(2, sut.Items[0].InstanceNumber);
    }

    [Fact]
    public void Refresh_RemovesMiddleSlotCorrectly()
    {
        // Regression: the previous enumeration-index approach would have
        // kept rows 1 and 2 then relabelled snap-for-slot-3 as row 2,
        // visually mis-attributing it.
        ReturnSlots((100, 1), (200, 2), (300, 3));
        var sut = CreateSut();
        sut.Refresh(new[] { Snap(100), Snap(200), Snap(300) });

        ReturnSlots((100, 1), (300, 3));
        sut.Refresh(new[] { Snap(100), Snap(300) });

        Assert.Equal(2, sut.Items.Count);
        Assert.Equal(1, sut.Items[0].InstanceNumber);
        Assert.Equal(3, sut.Items[1].InstanceNumber);
    }

    [Fact]
    public void Refresh_UsesSlotNameCallbackForNewRows()
    {
        ReturnSlots((100, 1));
        var sut = CreateSut();
        sut.GetSlotName = num => num == 1 ? "Primary" : "??";

        sut.Refresh(new[] { Snap(100) });

        Assert.Equal("Primary", sut.Items[0].SlotName);
    }

    [Fact]
    public void Refresh_FallsBackToInstanceNameWhenNoCallback()
    {
        ReturnSlots((100, 5));
        var sut = CreateSut();

        sut.Refresh(new[] { Snap(100) });

        Assert.Equal("Instance 5", sut.Items[0].SlotName);
    }

    [Fact]
    public void Refresh_PassesSeedStateFromInitialiser()
    {
        ReturnSlots((100, 1));
        _slotInitialiser.IsSeeded(new LaunchSlot(1)).Returns(true);
        var sut = CreateSut();

        sut.Refresh(new[] { Snap(100) });

        Assert.True(sut.Items[0].IsSeeded);
    }

    [Fact]
    public void Refresh_WhenScannerThrows_PreservesPreviousStateAndLogsWarning()
    {
        ReturnSlots((100, 1));
        var sut = CreateSut();
        sut.Refresh(new[] { Snap(100) });
        Assert.Single(sut.Items);

        _scanner.Scan().Returns<IReadOnlyList<ClaudeProcessInfo>>(
            _ => throw new InvalidOperationException("WMI dead"));
        sut.Refresh(new[] { Snap(100) });

        Assert.Single(sut.Items);
        _logger.Received(1).LogWarning(
            Arg.Is<string>(s => s.Contains("WMI dead")),
            Arg.Any<string>());
    }
}
