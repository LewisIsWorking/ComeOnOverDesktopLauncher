using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;

namespace ComeOnOverDesktopLauncher.Tests.Services;

public class SlotManagerTests
{
    private readonly IProcessService _processService = Substitute.For<IProcessService>();
    private SlotManager CreateSut() => new(_processService);

    [Fact]
    public void GetSlots_ReturnsCorrectCount()
    {
        Assert.Equal(3, CreateSut().GetSlots(3).Count);
    }

    [Fact]
    public void GetSlots_SlotsAreNumberedSequentially()
    {
        var slots = CreateSut().GetSlots(3);

        Assert.Equal(1, slots[0].SlotNumber);
        Assert.Equal(2, slots[1].SlotNumber);
        Assert.Equal(3, slots[2].SlotNumber);
    }

    [Fact]
    public void GetSlots_WhenCountIsZero_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateSut().GetSlots(0));
    }

    [Fact]
    public void GetNextAvailableSlot_UsesWindowedCountPlusOne()
    {
        _processService.CountByNameWithWindow("claude").Returns(2);

        Assert.Equal(3, CreateSut().GetNextAvailableSlot().SlotNumber);
    }

    [Fact]
    public void GetNextAvailableSlot_WhenNoneRunning_ReturnsSlotOne()
    {
        _processService.CountByNameWithWindow("claude").Returns(0);

        Assert.Equal(1, CreateSut().GetNextAvailableSlot().SlotNumber);
    }

    [Fact]
    public void GetNextAvailableSlot_DoesNotUseRawCount()
    {
        _processService.CountByNameWithWindow("claude").Returns(0);

        CreateSut().GetNextAvailableSlot();

        _processService.DidNotReceive().CountByName(Arg.Any<string>());
    }

    [Fact]
    public void GetNextFreeSlots_WhenNoneOccupied_ReturnsSlotsStartingFromOne()
    {
        _processService.GetSlotProcesses().Returns(new List<SlotProcessInfo>());

        var slots = CreateSut().GetNextFreeSlots(3);

        Assert.Equal(new[] { 1, 2, 3 }, slots.Select(s => s.SlotNumber));
    }

    [Fact]
    public void GetNextFreeSlots_SkipsOccupiedSlots()
    {
        _processService.GetSlotProcesses().Returns(new List<SlotProcessInfo>
        {
            new(100, 1),
            new(101, 3)
        });

        var slots = CreateSut().GetNextFreeSlots(3);

        Assert.Equal(new[] { 2, 4, 5 }, slots.Select(s => s.SlotNumber));
    }

    [Fact]
    public void GetNextFreeSlots_WhenCountIsZero_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateSut().GetNextFreeSlots(0));
    }

    [Fact]
    public void GetNextFreeSlots_WhenCountIsNegative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateSut().GetNextFreeSlots(-1));
    }

    [Fact]
    public void GetNextFreeSlots_WhenAllSlotsInRangeOccupied_Throws()
    {
        var occupied = Enumerable.Range(1, 100)
            .Select(n => new SlotProcessInfo(1000 + n, n))
            .ToList();
        _processService.GetSlotProcesses().Returns(occupied);

        Assert.Throws<InvalidOperationException>(() => CreateSut().GetNextFreeSlots(1));
    }

    [Fact]
    public void GetNextFreeSlots_DoesNotConsiderNonSlotProcesses()
    {
        // External Claude (default profile) is NOT returned by GetSlotProcesses,
        // so SlotManager should not see it and slot 1 should still be free.
        _processService.GetSlotProcesses().Returns(new List<SlotProcessInfo>());
        _processService.CountByNameWithWindow("claude").Returns(5);

        var slots = CreateSut().GetNextFreeSlots(1);

        Assert.Equal(1, slots[0].SlotNumber);
    }
}