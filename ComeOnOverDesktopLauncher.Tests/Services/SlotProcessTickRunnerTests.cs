using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;

namespace ComeOnOverDesktopLauncher.Tests.Services;

public class SlotProcessTickRunnerTests
{
    private readonly IProcessService _processService = Substitute.For<IProcessService>();
    private SlotProcessTickRunner CreateSut() => new(_processService);

    private static SlotProcessInfo Info(int slotNumber, int pid = 100) =>
        new(pid, slotNumber);

    [Fact]
    public void Tick_WhenNoProcesses_ReturnsEmpty()
    {
        _processService.GetSlotProcesses().Returns(Array.Empty<SlotProcessInfo>());
        Assert.Empty(CreateSut().Tick());
    }

    [Fact]
    public void Tick_FirstAppearance_DoesNotEmitClosedEvent()
    {
        _processService.GetSlotProcesses().Returns(new[] { Info(1) });
        Assert.Empty(CreateSut().Tick());
    }

    [Fact]
    public void Tick_WhenKnownSlotDisappears_EmitsClosedEvent()
    {
        var sut = CreateSut();
        _processService.GetSlotProcesses().Returns(new[] { Info(1) });
        sut.Tick(); // seed: slot 1 known

        _processService.GetSlotProcesses().Returns(Array.Empty<SlotProcessInfo>());
        var closed = sut.Tick();

        Assert.Single(closed);
        Assert.Equal(1, closed[0].SlotNumber);
    }

    [Fact]
    public void Tick_WhenOneOfManySlotsDisappears_EmitsOnlyThatOne()
    {
        var sut = CreateSut();
        _processService.GetSlotProcesses().Returns(new[] { Info(1, 100), Info(2, 200) });
        sut.Tick(); // seed: slots 1 and 2 known

        _processService.GetSlotProcesses().Returns(new[] { Info(2, 200) });
        var closed = sut.Tick();

        Assert.Single(closed);
        Assert.Equal(1, closed[0].SlotNumber);
    }

    [Fact]
    public void Tick_WhenSlotStaysRunning_NoEventEmitted()
    {
        var sut = CreateSut();
        _processService.GetSlotProcesses().Returns(new[] { Info(1) });
        sut.Tick();
        var closed = sut.Tick();
        Assert.Empty(closed);
    }

    [Fact]
    public void Tick_WhenOneSlotClosesAndAnotherStarts_EmitsOnlyClosed()
    {
        var sut = CreateSut();
        _processService.GetSlotProcesses().Returns(new[] { Info(1) });
        sut.Tick();
        _processService.GetSlotProcesses().Returns(new[] { Info(2) });

        var closed = sut.Tick();

        Assert.Single(closed);
        Assert.Equal(1, closed[0].SlotNumber);
    }
}