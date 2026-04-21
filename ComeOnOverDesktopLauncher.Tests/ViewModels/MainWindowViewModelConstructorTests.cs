using ComeOnOverDesktopLauncher.Core.Models;
using NSubstitute;

namespace ComeOnOverDesktopLauncher.Tests.ViewModels;

/// <summary>
/// Constructor-phase tests for <c>MainWindowViewModel</c> - these assert
/// state that should be loaded once at construction from settings, the
/// path resolver, the version resolver, and the startup service, so
/// they never execute commands or mutate observable properties.
/// </summary>
public class MainWindowViewModelConstructorTests
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
        Assert.Equal(3, _f.CreateSut().Resources.RunningInstanceCount);
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
        Assert.False(_f.CreateSut().Resources.HasRunningInstances);
    }

    [Fact]
    public void HasRunningInstances_WhenCountIsPositive_ReturnsTrue()
    {
        _f.Launcher.GetRunningInstanceCount().Returns(2);
        Assert.True(_f.CreateSut().Resources.HasRunningInstances);
    }
}
