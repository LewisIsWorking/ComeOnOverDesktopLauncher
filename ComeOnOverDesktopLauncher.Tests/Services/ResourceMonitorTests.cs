using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;

namespace ComeOnOverDesktopLauncher.Tests.Services;

public class ResourceMonitorTests
{
    private readonly IProcessService _processService = Substitute.For<IProcessService>();
    private ResourceMonitor CreateSut() => new(_processService);

    private static ProcessSnapshot MakeSnapshot(int pid, long ramBytes, double cpuMs, int secondsAgo = 60) =>
        new(pid, ramBytes, TimeSpan.FromMilliseconds(cpuMs),
            DateTime.UtcNow.AddSeconds(-secondsAgo), DateTime.UtcNow);

    [Fact]
    public void GetSnapshots_WhenNoProcesses_ReturnsEmpty()
    {
        _processService.GetWindowedProcessSnapshots("claude")
            .Returns(new List<ProcessSnapshot>());

        var result = CreateSut().GetSnapshots();

        Assert.Empty(result);
    }

    [Fact]
    public void GetSnapshots_AssignsInstanceNumbersSequentially()
    {
        _processService.GetWindowedProcessSnapshots("claude")
            .Returns(new List<ProcessSnapshot>
            {
                MakeSnapshot(1, 100_000_000, 0),
                MakeSnapshot(2, 200_000_000, 0)
            });

        var result = CreateSut().GetSnapshots();

        Assert.Equal(1, result[0].InstanceNumber);
        Assert.Equal(2, result[1].InstanceNumber);
    }

    [Fact]
    public void GetSnapshots_FirstCall_ReturnsCpuZero()
    {
        _processService.GetWindowedProcessSnapshots("claude")
            .Returns(new List<ProcessSnapshot> { MakeSnapshot(1, 100_000_000, 500) });

        var result = CreateSut().GetSnapshots();

        Assert.Equal(0.0, result[0].CpuPercent);
    }

    [Fact]
    public void GetSnapshots_CalculatesRamCorrectly()
    {
        _processService.GetWindowedProcessSnapshots("claude")
            .Returns(new List<ProcessSnapshot> { MakeSnapshot(1, 200 * 1024 * 1024, 0) });

        var result = CreateSut().GetSnapshots();

        Assert.Equal(200.0, result[0].RamMb);
    }

    [Fact]
    public void GetSnapshots_UpdatesTotalRamMb()
    {
        _processService.GetWindowedProcessSnapshots("claude")
            .Returns(new List<ProcessSnapshot>
            {
                MakeSnapshot(1, 100 * 1024 * 1024, 0),
                MakeSnapshot(2, 150 * 1024 * 1024, 0)
            });

        var sut = CreateSut();
        sut.GetSnapshots();

        Assert.Equal(250.0, sut.TotalRamMb);
    }

    [Fact]
    public void GetSnapshots_UptimeReflectsStartTime()
    {
        _processService.GetWindowedProcessSnapshots("claude")
            .Returns(new List<ProcessSnapshot> { MakeSnapshot(1, 0, 0, secondsAgo: 120) });

        var result = CreateSut().GetSnapshots();

        Assert.True(result[0].Uptime.TotalSeconds >= 100);
    }

    [Fact]
    public void GetSnapshots_WhenProcessDisappears_IsRemovedFromResults()
    {
        var withProcess = new List<ProcessSnapshot> { MakeSnapshot(1, 0, 0) };
        var withoutProcess = new List<ProcessSnapshot>();

        _processService.GetWindowedProcessSnapshots("claude")
            .Returns(withProcess, withoutProcess);

        var sut = CreateSut();
        sut.GetSnapshots();
        var result = sut.GetSnapshots();

        Assert.Empty(result);
    }
}
