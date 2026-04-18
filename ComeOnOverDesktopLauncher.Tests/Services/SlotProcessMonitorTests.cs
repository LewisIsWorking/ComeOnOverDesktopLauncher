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
        // Use a polling wait rather than a fixed Thread.Sleep. CI runners
        // can take several hundred milliseconds to deliver the first timer
        // tick under cold-start conditions; a short fixed sleep is flaky.
        _processService.GetSlotProcesses().Returns(Array.Empty<SlotProcessInfo>());
        using var sut = CreateSut();

        sut.Start(TimeSpan.FromMilliseconds(50));

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline &&
               _processService.ReceivedCalls().All(c => c.GetMethodInfo().Name != nameof(IProcessService.GetSlotProcesses)))
        {
            Thread.Sleep(50);
        }
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
        // If the second Start did not replace the first, GetSlotProcesses
        // would not be called within the polling window (the original 10s
        // timer would still be pending). We poll up to 10 seconds for a tick
        // from the replacement 50ms timer to keep the test fast but robust
        // against slow CI runners.
        using var sut = CreateSut();
        _processService.GetSlotProcesses().Returns(Array.Empty<SlotProcessInfo>());

        sut.Start(TimeSpan.FromSeconds(10));        // long interval
        sut.Start(TimeSpan.FromMilliseconds(50));   // replacement interval

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline &&
               _processService.ReceivedCalls().All(c => c.GetMethodInfo().Name != nameof(IProcessService.GetSlotProcesses)))
        {
            Thread.Sleep(50);
        }
        sut.Stop();

        _processService.ReceivedWithAnyArgs().GetSlotProcesses();
    }

    [Fact]
    public void SlotClosedEvent_RaisedForClosedSlots()
    {
        // On CI runners the first tick can take significantly longer than
        // 150 ms, so wait actively for the runner to observe "slot 1
        // running" before swapping in the "slot 1 gone" stub.
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

        // Wait up to 10 seconds for the runner to observe at least one tick
        // so that slot 1 becomes a known slot (otherwise there's nothing to
        // "close").
        var seedDeadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < seedDeadline &&
               _processService.ReceivedCalls().All(c => c.GetMethodInfo().Name != nameof(IProcessService.GetSlotProcesses)))
        {
            Thread.Sleep(50);
        }

        // Second tick onwards: slot 1 is gone
        _processService.GetSlotProcesses().Returns(Array.Empty<SlotProcessInfo>());

        var raised = eventRaised.Wait(TimeSpan.FromSeconds(10));
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

        // Poll for at least 2 calls (i.e. timer kept running after the
        // exception) rather than relying on a fixed sleep that can lose
        // to slow CI runners.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline && callCount < 2)
        {
            Thread.Sleep(50);
        }
        sut.Stop();

        _logger.Received().LogError(
            Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<string>());
        Assert.True(callCount >= 2,
            $"Timer should have kept polling after the exception, but only polled {callCount} time(s)");
    }
}