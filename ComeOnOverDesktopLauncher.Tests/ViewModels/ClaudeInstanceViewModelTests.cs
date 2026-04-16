using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.ViewModels;

namespace ComeOnOverDesktopLauncher.Tests.ViewModels;

public class ClaudeInstanceViewModelTests
{
    [Fact]
    public void Constructor_SetsInstanceNumber()
    {
        Assert.Equal(3, new ClaudeInstanceViewModel(3, "Work", isSeeded: true).InstanceNumber);
    }

    [Fact]
    public void Constructor_SetsInitialName()
    {
        Assert.Equal("Personal", new ClaudeInstanceViewModel(1, "Personal", isSeeded: true).SlotName);
    }

    [Fact]
    public void Constructor_SetsIsSeeded()
    {
        Assert.True(new ClaudeInstanceViewModel(1, "Work", isSeeded: true).IsSeeded);
        Assert.False(new ClaudeInstanceViewModel(2, "Home", isSeeded: false).IsSeeded);
    }

    [Fact]
    public void LoginStatusText_WhenSeeded_ShowsLoggedIn()
    {
        Assert.Equal("Logged in", new ClaudeInstanceViewModel(1, "Work", isSeeded: true).LoginStatusText);
    }

    [Fact]
    public void LoginStatusText_WhenNotSeeded_ShowsUnknown()
    {
        Assert.Equal("Not logged in", new ClaudeInstanceViewModel(1, "Work", isSeeded: false).LoginStatusText);
    }

    [Fact]
    public void LoginStatusTooltip_WhenSeeded_ShowsLoggedIn()
    {
        Assert.Contains("Logged in", new ClaudeInstanceViewModel(1, "Work", isSeeded: true).LoginStatusTooltip);
    }

    [Fact]
    public void LoginStatusTooltip_WhenNotSeeded_ShowsNotLoggedIn()
    {
        Assert.Contains("Not yet", new ClaudeInstanceViewModel(1, "Work", isSeeded: false).LoginStatusTooltip);
    }

    [Fact]
    public void SlotNameChange_InvokesCallback()
    {
        int? capturedSlot = null;
        string? capturedName = null;
        var vm = new ClaudeInstanceViewModel(2, "Old", isSeeded: false, onNameChanged: (slot, name) =>
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
        var vm = new ClaudeInstanceViewModel(1, "Test", isSeeded: false, onNameChanged: null);
        Assert.Null(Record.Exception(() => vm.SlotName = "Changed"));
    }

    [Fact]
    public void UpdateFrom_UpdatesAllResourceProperties()
    {
        var vm = new ClaudeInstanceViewModel(1, "Work", isSeeded: true);
        vm.UpdateFrom(new InstanceResourceSnapshot(123, 1, 12.5, 200 * 1024 * 1024, TimeSpan.FromMinutes(5)));

        Assert.Equal(12.5, vm.CpuPercent);
        Assert.Equal(200.0, vm.RamMb);
        Assert.Equal("5m 0s", vm.UptimeDisplay);
    }

    [Fact]
    public void UpdateFrom_DoesNotOverwriteSlotName()
    {
        var vm = new ClaudeInstanceViewModel(1, "Work", isSeeded: true);
        vm.UpdateFrom(new InstanceResourceSnapshot(1, 1, 5.0, 0, TimeSpan.Zero));
        Assert.Equal("Work", vm.SlotName);
    }

    [Fact]
    public void KillCommand_InvokesOnKillCallback()
    {
        int? capturedPid = null;
        var vm = new ClaudeInstanceViewModel(1, "Work", isSeeded: true, onKill: pid => capturedPid = pid);
        var snapshot = new InstanceResourceSnapshot(9999, 1, 0, 0, TimeSpan.Zero);
        vm.UpdateFrom(snapshot);

        vm.KillCommand.Execute(null);

        Assert.Equal(9999, capturedPid);
    }
}
