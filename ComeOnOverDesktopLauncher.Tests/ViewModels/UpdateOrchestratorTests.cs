using ComeOnOverDesktopLauncher.Core.Services.Interfaces;
using ComeOnOverDesktopLauncher.Services.Interfaces;
using ComeOnOverDesktopLauncher.ViewModels;
using NSubstitute;

namespace ComeOnOverDesktopLauncher.Tests.ViewModels;

/// <summary>
/// Exercises the <see cref="UpdateOrchestrator"/> state machine that
/// drives the v1.10.0+ Velopack auto-update UI.
///
/// <para>
/// The orchestrator is the brain of the update pipeline: it takes poll
/// ticks from <see cref="MainWindowUpdateViewModel"/>, asks
/// <see cref="IAutoUpdateService"/> to check/download, and emits
/// <see cref="UpdateUiState"/> transitions via the three callbacks it
/// was constructed with. Tests drive the orchestrator through its
/// states by stubbing the service responses and asserting the
/// resulting transition sequence.
/// </para>
///
/// <para>
/// The orchestrator is intentionally UI-framework-free (no
/// <c>Dispatcher</c>, no <c>ObservableProperty</c>) so tests don't
/// need Avalonia. The VM wraps it with dispatch plumbing.
/// </para>
/// </summary>
public class UpdateOrchestratorTests
{
    private readonly IAutoUpdateService _service = Substitute.For<IAutoUpdateService>();
    private readonly ILoggingService _logger = Substitute.For<ILoggingService>();
    private readonly List<UpdateUiState> _stateHistory = new();
    private readonly List<int> _progressHistory = new();
    private readonly List<string?> _versionHistory = new();

    private UpdateOrchestrator CreateSut() => new(
        _service, _logger,
        onStateChanged: s => _stateHistory.Add(s),
        onProgressChanged: p => _progressHistory.Add(p),
        onLatestVersionChanged: v => _versionHistory.Add(v));

    [Fact]
    public async Task RunCheckAsync_WhenAutoCheckDisabled_DoesNothing()
    {
        var sut = CreateSut();
        await sut.RunCheckAsync(autoCheckEnabled: false);

        await _service.DidNotReceive().CheckForUpdatesAsync();
        Assert.Empty(_stateHistory);
        Assert.Equal(UpdateUiState.Idle, sut.State);
    }

    [Fact]
    public async Task RunCheckAsync_WhenNoUpdate_TransitionsCheckingThenIdle()
    {
        _service.CheckForUpdatesAsync()
            .Returns(new UpdateCheckResult(UpdateStatus.NoUpdateAvailable));
        var sut = CreateSut();

        await sut.RunCheckAsync(autoCheckEnabled: true);

        Assert.Equal(new[] { UpdateUiState.Checking, UpdateUiState.Idle }, _stateHistory);
        Assert.Empty(_versionHistory);
    }

    [Fact]
    public async Task RunCheckAsync_WhenCheckFails_TransitionsToFailed()
    {
        _service.CheckForUpdatesAsync()
            .Returns(new UpdateCheckResult(UpdateStatus.Failed, Error: "offline"));
        var sut = CreateSut();

        await sut.RunCheckAsync(autoCheckEnabled: true);

        Assert.Equal(new[] { UpdateUiState.Checking, UpdateUiState.Failed }, _stateHistory);
        Assert.Equal(UpdateUiState.Failed, sut.State);
    }

    [Fact]
    public async Task RunCheckAsync_WhenUpdateAvailable_RunsDownloadAndReachesReady()
    {
        _service.CheckForUpdatesAsync()
            .Returns(new UpdateCheckResult(UpdateStatus.UpdateAvailable, LatestVersion: "1.10.1"));
        _service.DownloadUpdatesAsync(Arg.Any<IProgress<int>?>())
            .Returns(true);
        var sut = CreateSut();

        await sut.RunCheckAsync(autoCheckEnabled: true);

        Assert.Equal(
            new[] { UpdateUiState.Checking, UpdateUiState.Downloading, UpdateUiState.ReadyToInstall },
            _stateHistory);
        Assert.Equal(new[] { (string?)"1.10.1" }, _versionHistory);
        Assert.Contains(0, _progressHistory);
    }

    [Fact]
    public async Task RunCheckAsync_WhenDownloadFails_TransitionsToFailed()
    {
        _service.CheckForUpdatesAsync()
            .Returns(new UpdateCheckResult(UpdateStatus.UpdateAvailable, LatestVersion: "1.10.1"));
        _service.DownloadUpdatesAsync(Arg.Any<IProgress<int>?>()).Returns(false);
        var sut = CreateSut();

        await sut.RunCheckAsync(autoCheckEnabled: true);

        Assert.Equal(
            new[] { UpdateUiState.Checking, UpdateUiState.Downloading, UpdateUiState.Failed },
            _stateHistory);
    }

    [Fact]
    public async Task RunCheckAsync_WhenAlreadyReadyToInstall_SkipsNewCheck()
    {
        _service.CheckForUpdatesAsync()
            .Returns(new UpdateCheckResult(UpdateStatus.UpdateAvailable, LatestVersion: "1.10.1"));
        _service.DownloadUpdatesAsync(Arg.Any<IProgress<int>?>()).Returns(true);
        var sut = CreateSut();
        await sut.RunCheckAsync(autoCheckEnabled: true);
        _service.ClearReceivedCalls();

        await sut.RunCheckAsync(autoCheckEnabled: true);

        await _service.DidNotReceive().CheckForUpdatesAsync();
        Assert.Equal(UpdateUiState.ReadyToInstall, sut.State);
    }

    [Fact]
    public void ApplyAndRestart_WhenReady_CallsServiceExactlyOnce()
    {
        _service.CheckForUpdatesAsync()
            .Returns(new UpdateCheckResult(UpdateStatus.UpdateAvailable, LatestVersion: "1.10.1"));
        _service.DownloadUpdatesAsync(Arg.Any<IProgress<int>?>()).Returns(true);
        var sut = CreateSut();
        sut.RunCheckAsync(autoCheckEnabled: true).GetAwaiter().GetResult();

        sut.ApplyAndRestart();

        _service.Received(1).ApplyUpdatesAndRestart();
    }

    [Fact]
    public void ApplyAndRestart_WhenNotReady_DoesNothing()
    {
        var sut = CreateSut();

        sut.ApplyAndRestart();

        _service.DidNotReceive().ApplyUpdatesAndRestart();
    }

    [Fact]
    public async Task Retry_AfterFailed_ResetsToIdleSoNextCheckCanRun()
    {
        _service.CheckForUpdatesAsync()
            .Returns(new UpdateCheckResult(UpdateStatus.Failed, Error: "offline"));
        var sut = CreateSut();
        await sut.RunCheckAsync(autoCheckEnabled: true);

        sut.Retry();

        Assert.Equal(UpdateUiState.Idle, sut.State);
    }

    [Fact]
    public void Retry_WhenNotFailed_LeavesStateAlone()
    {
        var sut = CreateSut();

        sut.Retry();

        Assert.Equal(UpdateUiState.Idle, sut.State);
        Assert.Empty(_stateHistory);
    }
}
