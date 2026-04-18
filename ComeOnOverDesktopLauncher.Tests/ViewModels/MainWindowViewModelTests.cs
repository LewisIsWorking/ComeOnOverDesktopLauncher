using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.ViewModels;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ComeOnOverDesktopLauncher.Tests.ViewModels;

public class MainWindowViewModelTests
{
    private readonly MainWindowViewModelTestFixture _f = new();

    [Fact]
    public void Constructor_LoadsSlotCountFromSettings()
    {
        Assert.Equal(2, _f.CreateSut().SlotCount);
    }

    [Fact]
    public void Constructor_SetsAppVersionFromProvider()
    {
        Assert.Equal("v1.3.0", _f.CreateSut().AppVersion);
    }

    [Fact]
    public void Constructor_SetsClaudeVersionFromResolver()
    {
        _f.ClaudeVersionResolver.GetClaudeVersion().Returns("1.3109.0.0");
        Assert.Equal("1.3109.0.0", _f.CreateSut().ClaudeVersion);
    }

    [Fact]
    public void Constructor_WhenClaudeVersionIsNull_FooterShowsAppVersionOnly()
    {
        _f.ClaudeVersionResolver.GetClaudeVersion().Returns((string?)null);
        Assert.Equal("v1.3.0", _f.CreateSut().FooterVersionText);
    }

    [Fact]
    public void Constructor_WhenClaudeVersionIsKnown_FooterShowsBoth()
    {
        _f.ClaudeVersionResolver.GetClaudeVersion().Returns("1.3109.0.0");
        Assert.Equal("v1.3.0 - Claude 1.3109.0.0", _f.CreateSut().FooterVersionText);
    }

    [Fact]
    public void Constructor_SetsLaunchOnStartupFromStartupService()
    {
        _f.StartupService.IsStartupEnabled().Returns(true);
        Assert.True(_f.CreateSut().LaunchOnStartup);
    }

    [Fact]
    public void Constructor_SetsIsClaudeInstalledFromResolver()
    {
        _f.PathResolver.IsClaudeInstalled().Returns(true);
        Assert.True(_f.CreateSut().IsClaudeInstalled);
    }

    [Fact]
    public void Constructor_LoadsRunningInstanceCount()
    {
        _f.Launcher.GetRunningInstanceCount().Returns(3);
        Assert.Equal(3, _f.CreateSut().RunningInstanceCount);
    }

    [Fact]
    public void Constructor_ExternalInstancesIsNonNull()
    {
        Assert.NotNull(_f.CreateSut().ExternalInstances);
    }

    [Fact]
    public void Constructor_SlotInstancesIsNonNull()
    {
        Assert.NotNull(_f.CreateSut().SlotInstances);
    }

    [Fact]
    public void HasRunningInstances_WhenCountIsZero_ReturnsFalse()
    {
        _f.Launcher.GetRunningInstanceCount().Returns(0);
        Assert.False(_f.CreateSut().HasRunningInstances);
    }

    [Fact]
    public void HasRunningInstances_WhenCountIsPositive_ReturnsTrue()
    {
        _f.Launcher.GetRunningInstanceCount().Returns(2);
        Assert.True(_f.CreateSut().HasRunningInstances);
    }

    [Fact]
    public void LaunchOnStartup_WhenSetToTrue_CallsEnableStartup()
    {
        var sut = _f.CreateSut();
        sut.LaunchOnStartup = true;
        _f.StartupService.Received().EnableStartup(Arg.Any<string>());
    }

    [Fact]
    public void LaunchOnStartup_WhenSetToFalse_CallsDisableStartup()
    {
        _f.StartupService.IsStartupEnabled().Returns(true);
        var sut = _f.CreateSut();
        sut.LaunchOnStartup = false;
        _f.StartupService.Received().DisableStartup();
    }

    [Fact]
    public void LaunchInstancesCommand_DelegatesToLauncher()
    {
        _f.Launcher.LaunchInstances(Arg.Any<int>()).Returns([new LaunchSlot(1), new LaunchSlot(2)]);
        var sut = _f.CreateSut();
        sut.SlotCount = 2;

        sut.LaunchInstancesCommand.Execute(null);

        _f.Launcher.Received(1).LaunchInstances(2);
    }

    [Fact]
    public void LaunchInstancesCommand_UpdatesRunningInstanceCount()
    {
        _f.Launcher.LaunchInstances(Arg.Any<int>()).Returns([new LaunchSlot(1)]);
        _f.Launcher.GetRunningInstanceCount().Returns(2);
        var sut = _f.CreateSut();

        sut.LaunchInstancesCommand.Execute(null);

        Assert.Equal(2, sut.RunningInstanceCount);
    }

    [Fact]
    public void LaunchInstancesCommand_UpdatesStatusMessageWithLaunchedCount()
    {
        _f.Launcher.LaunchInstances(Arg.Any<int>()).Returns([new LaunchSlot(1), new LaunchSlot(2)]);
        var sut = _f.CreateSut();

        sut.LaunchInstancesCommand.Execute(null);

        Assert.Contains("2 instance", sut.StatusMessage);
    }

    [Fact]
    public void LaunchInstancesCommand_WhenExceptionThrown_SetsErrorStatusMessage()
    {
        _f.Launcher.LaunchInstances(Arg.Any<int>()).Throws(new InvalidOperationException("Test error"));
        var sut = _f.CreateSut();

        sut.LaunchInstancesCommand.Execute(null);

        Assert.Contains("Error", sut.StatusMessage);
    }

    [Fact]
    public void LaunchComeOnOverCommand_LaunchesCooService()
    {
        _f.CreateSut().LaunchComeOnOverCommand.Execute(null);
        _f.CooService.Received(1).Launch();
    }

    [Fact]
    public void LaunchComeOnOverCommand_UpdatesStatusMessage()
    {
        var sut = _f.CreateSut();
        sut.LaunchComeOnOverCommand.Execute(null);
        Assert.Contains("ComeOnOver", sut.StatusMessage);
    }

    [Fact]
    public void RefreshResourcesCommand_UpdatesTotalsFromMonitor()
    {
        _f.ResourceMonitor.GetSnapshots().Returns(new List<InstanceResourceSnapshot>());
        _f.ResourceMonitor.TotalRamMb.Returns(512.0);
        _f.ResourceMonitor.TotalCpuPercent.Returns(8.5);
        var sut = _f.CreateSut();

        sut.RefreshResourcesCommand.Execute(null);

        Assert.Equal(512.0, sut.TotalRamMb);
        Assert.Equal(8.5, sut.TotalCpuPercent);
    }

    [Fact]
    public void RefreshResourcesCommand_RefreshesSlotInstances()
    {
        _f.ResourceMonitor.GetSnapshots().Returns(new List<InstanceResourceSnapshot>());
        _f.Scanner.Scan().Returns(Array.Empty<ClaudeProcessInfo>());
        var sut = _f.CreateSut();

        sut.RefreshResourcesCommand.Execute(null);

        // Slot VM + external VM both use the scanner on refresh, so we
        // expect at least 2 calls (one from each list VM).
        Assert.True(_f.Scanner.ReceivedCalls().Count() >= 2);
    }

    [Fact]
    public void SaveSettingsCommand_PersistsCurrentSlotCount()
    {
        var sut = _f.CreateSut();
        sut.SlotCount = 4;

        sut.SaveSettingsCommand.Execute(null);

        _f.SettingsService.Received().Save(Arg.Is<AppSettings>(s => s.DefaultSlotCount == 4));
    }
}
