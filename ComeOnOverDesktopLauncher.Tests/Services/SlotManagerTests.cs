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
}
