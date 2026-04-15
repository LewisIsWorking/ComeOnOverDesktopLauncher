using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.ViewModels;

namespace ComeOnOverDesktopLauncher.Tests.ViewModels;

public class ClaudeInstanceViewModelTests
{
    [Fact]
    public void Constructor_SetsInstanceNumber()
    {
        var vm = new ClaudeInstanceViewModel(3, "Work");

        Assert.Equal(3, vm.InstanceNumber);
    }

    [Fact]
    public void Constructor_SetsInitialName()
    {
        var vm = new ClaudeInstanceViewModel(1, "Personal");

        Assert.Equal("Personal", vm.SlotName);
    }

    [Fact]
    public void Constructor_DefaultsToEmptyNameWhenNotProvided()
    {
        var vm = new ClaudeInstanceViewModel(1, string.Empty);

        Assert.Equal(string.Empty, vm.SlotName);
    }

    [Fact]
    public void SlotNameChange_InvokesCallback()
    {
        int? capturedSlot = null;
        string? capturedName = null;
        var vm = new ClaudeInstanceViewModel(2, "Old", (slot, name) =>
        {
            capturedSlot = slot;
            capturedName = name;
        });

        vm.SlotName = "New Name";

        Assert.Equal(2, capturedSlot);
        Assert.Equal("New Name", capturedName);
    }

    [Fact]
    public void SlotNameChange_WithNoCallback_DoesNotThrow()
    {
        var vm = new ClaudeInstanceViewModel(1, "Test", null);

        var ex = Record.Exception(() => vm.SlotName = "Changed");

        Assert.Null(ex);
    }

    [Fact]
    public void UpdateFrom_UpdatesAllResourceProperties()
    {
        var vm = new ClaudeInstanceViewModel(1, "Work");
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
    public void UpdateFrom_DoesNotOverwriteSlotName()
    {
        var vm = new ClaudeInstanceViewModel(1, "Work");
        var snapshot = new InstanceResourceSnapshot(1, 1, 5.0, 0, TimeSpan.Zero);

        vm.UpdateFrom(snapshot);

        Assert.Equal("Work", vm.SlotName);
    }
}
