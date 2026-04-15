using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;
using ComeOnOverDesktopLauncher.ViewModels;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ComeOnOverDesktopLauncher.Tests.ViewModels;

public class MainWindowViewModelTests
{
    private readonly IClaudeInstanceLauncher _launcher = Substitute.For<IClaudeInstanceLauncher>();
    private readonly ISlotManager _slotManager = Substitute.For<ISlotManager>();
    private readonly IComeOnOverAppService _cooService = Substitute.For<IComeOnOverAppService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IClaudePathResolver _pathResolver = Substitute.For<IClaudePathResolver>();
    private readonly IResourceMonitor _resourceMonitor = Substitute.For<IResourceMonitor>();

    private MainWindowViewModel CreateSut()
    {
        _settingsService.Load().Returns(new AppSettings { DefaultSlotCount = 2 });
        return new MainWindowViewModel(
            _launcher, _slotManager, _cooService,
            _settingsService, _pathResolver, _resourceMonitor);
    }

    [Fact]
    public void Constructor_LoadsSlotCountFromSettings()
    {
        Assert.Equal(2, CreateSut().SlotCount);
    }

    [Fact]
    public void Constructor_SetsIsClaudeInstalledFromResolver()
    {
        _pathResolver.IsClaudeInstalled().Returns(true);

        Assert.True(CreateSut().IsClaudeInstalled);
    }

    [Fact]
    public void Constructor_LoadsRunningInstanceCount()
    {
        _launcher.GetRunningInstanceCount().Returns(3);

        Assert.Equal(3, CreateSut().RunningInstanceCount);
    }

    [Fact]
    public void HasRunningInstances_WhenCountIsZero_ReturnsFalse()
    {
        _launcher.GetRunningInstanceCount().Returns(0);

        Assert.False(CreateSut().HasRunningInstances);
    }

    [Fact]
    public void HasRunningInstances_WhenCountIsPositive_ReturnsTrue()
    {
        _launcher.GetRunningInstanceCount().Returns(2);

        Assert.True(CreateSut().HasRunningInstances);
    }

    [Fact]
    public void LaunchInstancesCommand_LaunchesCorrectNumberOfSlots()
    {
        _slotManager.GetSlots(2).Returns([new LaunchSlot(1), new LaunchSlot(2)]);
        var sut = CreateSut();

        sut.LaunchInstancesCommand.Execute(null);

        _launcher.Received(2).LaunchSlot(Arg.Any<LaunchSlot>());
    }

    [Fact]
    public void LaunchInstancesCommand_UpdatesRunningInstanceCount()
    {
        _slotManager.GetSlots(Arg.Any<int>()).Returns([new LaunchSlot(1)]);
        _launcher.GetRunningInstanceCount().Returns(2);
        var sut = CreateSut();

        sut.LaunchInstancesCommand.Execute(null);

        Assert.Equal(2, sut.RunningInstanceCount);
    }

    [Fact]
    public void LaunchInstancesCommand_WhenExceptionThrown_SetsErrorStatusMessage()
    {
        _slotManager.GetSlots(Arg.Any<int>()).Throws(new InvalidOperationException("Test error"));
        var sut = CreateSut();

        sut.LaunchInstancesCommand.Execute(null);

        Assert.Contains("Error", sut.StatusMessage);
    }

    [Fact]
    public void LaunchComeOnOverCommand_LaunchesCooService()
    {
        CreateSut().LaunchComeOnOverCommand.Execute(null);

        _cooService.Received(1).Launch();
    }

    [Fact]
    public void LaunchComeOnOverCommand_UpdatesStatusMessage()
    {
        var sut = CreateSut();

        sut.LaunchComeOnOverCommand.Execute(null);

        Assert.Contains("ComeOnOver", sut.StatusMessage);
    }

    [Fact]
    public void RefreshResourcesCommand_UpdatesTotalsFromMonitor()
    {
        _resourceMonitor.GetSnapshots().Returns(new List<InstanceResourceSnapshot>());
        _resourceMonitor.TotalRamMb.Returns(512.0);
        _resourceMonitor.TotalCpuPercent.Returns(8.5);
        var sut = CreateSut();

        sut.RefreshResourcesCommand.Execute(null);

        Assert.Equal(512.0, sut.TotalRamMb);
        Assert.Equal(8.5, sut.TotalCpuPercent);
    }

    [Fact]
    public void RefreshResourcesCommand_SyncsInstanceCollection()
    {
        var sut = CreateSut();
        var snapshots = new List<InstanceResourceSnapshot>
        {
            new(1, 1, 5.0, 200 * 1024 * 1024, TimeSpan.FromMinutes(10))
        };
        _resourceMonitor.GetSnapshots().Returns(snapshots);

        sut.RefreshResourcesCommand.Execute(null);

        Assert.Single(sut.Instances);
        Assert.Equal(5.0, sut.Instances[0].CpuPercent);
    }

    [Fact]
    public void SaveSettingsCommand_PersistsCurrentSlotCount()
    {
        var sut = CreateSut();
        sut.SlotCount = 4;

        sut.SaveSettingsCommand.Execute(null);

        _settingsService.Received().Save(Arg.Is<AppSettings>(s => s.DefaultSlotCount == 4));
    }
}
