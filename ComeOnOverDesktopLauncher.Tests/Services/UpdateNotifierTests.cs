using ComeOnOverDesktopLauncher.Core.Services;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;

namespace ComeOnOverDesktopLauncher.Tests.Services;

public class UpdateNotifierTests
{
    private readonly IUpdateChecker _updateChecker = Substitute.For<IUpdateChecker>();
    private readonly IVersionProvider _versionProvider = Substitute.For<IVersionProvider>();

    private UpdateNotifier CreateSut() => new(_updateChecker, _versionProvider);

    [Fact]
    public async Task GetUpdateAvailableMessageAsync_WhenNoLatestVersion_ReturnsNull()
    {
        _updateChecker.GetLatestVersionAsync().Returns(Task.FromResult<string?>(null));

        var result = await CreateSut().GetUpdateAvailableMessageAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task GetUpdateAvailableMessageAsync_WhenLatestIsNotNewer_ReturnsNull()
    {
        _updateChecker.GetLatestVersionAsync().Returns(Task.FromResult<string?>("1.7.1"));
        _versionProvider.GetVersion().Returns("1.7.1");
        _updateChecker.IsNewerVersion("1.7.1", "1.7.1").Returns(false);

        var result = await CreateSut().GetUpdateAvailableMessageAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task GetUpdateAvailableMessageAsync_WhenLatestIsNewer_ReturnsFormattedMessage()
    {
        _updateChecker.GetLatestVersionAsync().Returns(Task.FromResult<string?>("1.8.0"));
        _versionProvider.GetVersion().Returns("1.7.1");
        _updateChecker.IsNewerVersion("1.7.1", "1.8.0").Returns(true);

        var result = await CreateSut().GetUpdateAvailableMessageAsync();

        Assert.NotNull(result);
        Assert.Contains("1.8.0", result);
        Assert.Contains("github.com/LewisIsWorking/ComeOnOverDesktopLauncher", result);
    }
}