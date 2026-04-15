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

    private MainWindowViewModel CreateSut()
    {
        _settingsService.Load().Returns(new AppSettings { DefaultSlotCount = 2 });
        return new MainWindowViewModel(_launcher, _slotManager, _cooService, _settingsService, _pathResolver);
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
    public void LaunchInstancesCommand_LaunchesCorrectNumberOfSlots()
    {
        _slotManager.GetSlots(2).Returns([new LaunchSlot(1), new LaunchSlot(2)]);
        var sut = CreateSut();

        sut.LaunchInstancesCommand.Execute(null);

        _launcher.Received(2).LaunchSlot(Arg.Any<LaunchSlot>());
    }

    [Fact]
    public void LaunchInstancesCommand_UpdatesStatusMessage()
    {
        _slotManager.GetSlots(Arg.Any<int>()).Returns([new LaunchSlot(1), new LaunchSlot(2)]);
        var sut = CreateSut();

        sut.LaunchInstancesCommand.Execute(null);

        Assert.Contains("2", sut.StatusMessage);
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
    public void SaveSettingsCommand_PersistsCurrentSlotCount()
    {
        var sut = CreateSut();
        sut.SlotCount = 4;

        sut.SaveSettingsCommand.Execute(null);

        _settingsService.Received().Save(Arg.Is<AppSettings>(s => s.DefaultSlotCount == 4));
    }
}
