using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;

namespace ComeOnOverDesktopLauncher.Tests.Services;

public class ClaudeInstanceLauncherTests
{
    private readonly IClaudePathResolver _pathResolver = Substitute.For<IClaudePathResolver>();
    private readonly IProcessService _processService = Substitute.For<IProcessService>();
    private readonly ILoggingService _logger = Substitute.For<ILoggingService>();
    private ClaudeInstanceLauncher CreateSut() => new(_pathResolver, _processService, _logger);

    [Fact]
    public void LaunchSlot_WhenClaudeFound_StartsProcess()
    {
        _pathResolver.ResolveClaudeExePath().Returns(@"C:\claude.exe");

        CreateSut().LaunchSlot(new LaunchSlot(1));

        _processService.Received(1).Start(
            @"C:\claude.exe",
            Arg.Is<string>(a => a.Contains("ClaudeSlot1")));
    }

    [Fact]
    public void LaunchSlot_WhenClaudeNotFound_ThrowsInvalidOperationException()
    {
        _pathResolver.ResolveClaudeExePath().Returns((string?)null);

        Assert.Throws<InvalidOperationException>(() => CreateSut().LaunchSlot(new LaunchSlot(1)));
    }

    [Fact]
    public void GetRunningInstanceCount_UsesWindowedCount()
    {
        _processService.CountByNameWithWindow("claude").Returns(3);

        Assert.Equal(3, CreateSut().GetRunningInstanceCount());
    }

    [Fact]
    public void GetRunningInstanceCount_DoesNotUseRawCount()
    {
        _processService.CountByNameWithWindow("claude").Returns(3);

        CreateSut().GetRunningInstanceCount();

        _processService.DidNotReceive().CountByName(Arg.Any<string>());
    }

    [Fact]
    public void KillInstance_DelegatesToProcessService()
    {
        CreateSut().KillInstance(1234);

        _processService.Received(1).KillProcess(1234);
    }
}
