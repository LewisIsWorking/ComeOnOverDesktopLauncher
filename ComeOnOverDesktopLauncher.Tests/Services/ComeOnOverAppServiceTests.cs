using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;

namespace ComeOnOverDesktopLauncher.Tests.Services;

public class ComeOnOverAppServiceTests
{
    private readonly IProcessService _processService = Substitute.For<IProcessService>();
    private readonly AppSettings _settings = new() { ComeOnOverUrl = "https://comeonover.app" };
    private ComeOnOverAppService CreateSut() => new(_processService, _settings);

    [Fact]
    public void Launch_OpensConfiguredUrl()
    {
        CreateSut().Launch();

        _processService.Received(1).Start(
            "https://comeonover.app",
            useShellExecute: true);
    }

    [Fact]
    public void IsInstalled_ReturnsFalse()
    {
        Assert.False(CreateSut().IsInstalled());
    }
}
