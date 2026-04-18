using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;

namespace ComeOnOverDesktopLauncher.Tests.Services;

/// <summary>
/// Lifecycle tests for the timer-driven <see cref="SlotProcessMonitor"/>.
/// The pure transition logic is covered by <see cref="SlotProcessTickRunnerTests"/>;
/// these tests focus on Start/Stop/Dispose semantics and that exceptions
/// thrown during a tick do not tear down the timer.
/// </summary>
public class SlotProcessMonitorTests
{
    private readonly IProcessService _processService = Substitute.For<IProcessService>();
    private readonly ILoggingService _logger = Substitute.For<ILoggingService>();

    private SlotProcessMonitor CreateSut() => new(_processService, _logger);

    [Fact]
    public void Start_BeginsPolling()
    {
        _processService.GetSlotProcesses().Returns(Array.Empty<SlotProcessInfo>());
        using var sut = CreateSut();

        sut.Start(TimeSpan.FromMilliseconds(50));

        // Wait long enough for at least one tick to happen
        Thread.Sleep(200);
        sut.Stop();

        _processService.ReceivedWithAnyArgs().GetSlotProcesses();
    }

    [Fact]
    public void Stop_WhenNotStarted_DoesNothing()
    {
        using var sut = CreateSut();
        sut.Stop();
        // No exception - passes
    }

    [Fact]
    public void Dispose_StopsTheTimer()
    {
        var sut = CreateSut();
        _processService.GetSlotProcesses().Returns(Array.Empty<SlotProcessInfo>());
        sut.Start(TimeSpan.FromMilliseconds(50));

        sut.Dispose();
        _processService.ClearReceivedCalls();

        // After dispose, no more ticks should happen
        Thread.Sleep(200);
        _processService.DidNotReceiveWithAnyArgs().GetSlotProcesses();
    }

    [Fact]
    public void Start_CalledTwice_ReplacesPreviousTimer()
    {
        using var sut = CreateSut();
        _processService.GetSlotProcesses().Returns(Array.Empty<SlotProcessInfo>());

        sut.Start(TimeSpan.FromSeconds(10)); // long interval - would rarely tick
        sut.Start(TimeSpan.FromMilliseconds(50)); // short interval - ticks quickly

        Thread.Sleep(200);
        sut.Stop();

        _processService.ReceivedWithAnyArgs().GetSlotProcesses();
    }

    [Fact]
    public void SlotClosedEvent_RaisedForClosedSlots()
    {
        using var sut = CreateSut();
        LaunchSlot? closedSlot = null;
        var eventRaised = new ManualResetEventSlim(false);

        sut.SlotClosed += (_, args) =>
        {
            closedSlot = args.Slot;
            eventRaised.Set();
        };

        // First tick: slot 1 is running
        _processService.GetSlotProcesses().Returns(new[] { new SlotProcessInfo(100, 1) });
        sut.Start(TimeSpan.FromMilliseconds(50));

        // Wait for at least one tick to seed the runner
        Thread.Sleep(150);

        // Second tick onwards: slot 1 is gone
        _processService.GetSlotProcesses().Returns(Array.Empty<SlotProcessInfo>());

        var raised = eventRaised.Wait(TimeSpan.FromSeconds(2));
        sut.Stop();

        Assert.True(raised, "SlotClosed event should have been raised");
        Assert.NotNull(closedSlot);
        Assert.Equal(1, closedSlot!.SlotNumber);
    }

    [Fact]
    public void Tick_WhenProcessServiceThrows_ErrorIsLoggedAndTimerKeepsRunning()
    {
        using var sut = CreateSut();
        var callCount = 0;
        _processService.GetSlotProcesses().Returns(_ =>
        {
            callCount++;
            if (callCount == 1) throw new InvalidOperationException("boom");
            return Array.Empty<SlotProcessInfo>();
        });

        sut.Start(TimeSpan.FromMilliseconds(50));
        Thread.Sleep(250); // give time for multiple ticks
        sut.Stop();

        _logger.Received().LogError(
            Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<string>());
        Assert.True(callCount >= 2,
            $"Timer should have kept polling after the exception, but only polled {callCount} time(s)");
    }
}