using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.ViewModels;

namespace ComeOnOverDesktopLauncher.Tests.ViewModels;

public class ClaudeInstanceViewModelTests
{
    [Fact]
    public void Constructor_SetsInstanceNumber()
    {
        var vm = new ClaudeInstanceViewModel(3);

        Assert.Equal(3, vm.InstanceNumber);
    }

    [Fact]
    public void UpdateFrom_UpdatesAllProperties()
    {
        var vm = new ClaudeInstanceViewModel(1);
        var snapshot = new InstanceResourceSnapshot(
            ProcessId: 123,
            InstanceNumber: 1,
            CpuPercent: 12.5,
            RamBytes: 200 * 1024 * 1024,
            Uptime: TimeSpan.FromMinutes(5));

        vm.UpdateFrom(snapshot);

        Assert.Equal(12.5, vm.CpuPercent);
        Assert.Equal(200.0, vm.RamMb);
        Assert.Equal("5m 0s", vm.UptimeDisplay);
    }

    [Fact]
    public void UpdateFrom_CanBeCalledMultipleTimes()
    {
        var vm = new ClaudeInstanceViewModel(1);
        var snap1 = new InstanceResourceSnapshot(1, 1, 5.0, 100 * 1024 * 1024, TimeSpan.FromMinutes(1));
        var snap2 = new InstanceResourceSnapshot(1, 1, 15.0, 200 * 1024 * 1024, TimeSpan.FromMinutes(2));

        vm.UpdateFrom(snap1);
        vm.UpdateFrom(snap2);

        Assert.Equal(15.0, vm.CpuPercent);
        Assert.Equal(200.0, vm.RamMb);
    }
}
