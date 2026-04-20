using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;
using ComeOnOverDesktopLauncher.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.ViewModels;

/// <summary>
/// Sub-VM for the v1.10.0+ Velopack-driven auto-update UI. Owns the
/// <see cref="UpdateOrchestrator"/>, exposes observable state to the
/// update banner in <c>LaunchControlsPanel</c>, and hosts the three
/// user-driven update commands. Extracted from
/// <see cref="MainWindowViewModel"/> so the root VM stays under the
/// 200-line limit; same split pattern as <c>SlotInstanceListViewModel</c>
/// and <c>ExternalInstanceListViewModel</c>.
///
/// <para>
/// Bound in XAML via <c>Update.IsReadyToInstall</c>,
/// <c>Update.DownloadingBannerText</c>, <c>Update.RestartCommand</c>,
/// etc. The root VM exposes this as a non-nullable <c>Update</c> property.
/// </para>
/// </summary>
public partial class MainWindowUpdateViewModel : ObservableObject
{
    private const string GithubRepoBase =
        "https://github.com/LewisIsWorking/ComeOnOverDesktopLauncher";

    private readonly UpdateOrchestrator _orchestrator;
    private readonly IProcessService _processService;
    private readonly DispatcherTimer _checkTimer;

    [ObservableProperty] private bool _autoCheckEnabled;
    [ObservableProperty] private UpdateUiState _state;
    [ObservableProperty] private int _downloadProgress;
    [ObservableProperty] private string? _latestVersion;

    public bool IsDownloading => State == UpdateUiState.Downloading;
    public bool IsReadyToInstall => State == UpdateUiState.ReadyToInstall;
    public bool IsFailed => State == UpdateUiState.Failed;
    public string ReadyBannerText => LatestVersion is null
        ? "Update ready - restart to install"
        : $"v{LatestVersion} ready - restart to install";
    public string DownloadingBannerText => LatestVersion is null
        ? $"Downloading update... {DownloadProgress}%"
        : $"Downloading v{LatestVersion}... {DownloadProgress}%";
    public string ReleaseNotesUrl => LatestVersion is null
        ? $"{GithubRepoBase}/releases"
        : $"{GithubRepoBase}/releases/tag/v{LatestVersion}";

    public MainWindowUpdateViewModel(
        IAutoUpdateService autoUpdateService,
        IProcessService processService,
        ILoggingService logger,
        AppSettings settings,
        Action onSettingsChanged)
    {
        _processService = processService;
        _autoCheckEnabled = settings.AutoCheckForUpdates;
        _orchestrator = new UpdateOrchestrator(
            autoUpdateService, logger,
            onStateChanged: s => Dispatcher.UIThread.Post(() => State = s),
            onProgressChanged: p => Dispatcher.UIThread.Post(() => DownloadProgress = p),
            onLatestVersionChanged: v => Dispatcher.UIThread.Post(() => LatestVersion = v));

        _checkTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(6) };
        _checkTimer.Tick += async (_, _) => await CheckAsync();
        _checkTimer.Start();
        _ = CheckAsync();

        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(AutoCheckEnabled)) return;
            settings.AutoCheckForUpdates = AutoCheckEnabled;
            onSettingsChanged();
        };
    }

    partial void OnStateChanged(UpdateUiState value)
    {
        OnPropertyChanged(nameof(IsDownloading));
        OnPropertyChanged(nameof(IsReadyToInstall));
        OnPropertyChanged(nameof(IsFailed));
    }

    partial void OnLatestVersionChanged(string? value)
    {
        OnPropertyChanged(nameof(ReadyBannerText));
        OnPropertyChanged(nameof(DownloadingBannerText));
        OnPropertyChanged(nameof(ReleaseNotesUrl));
    }

    partial void OnDownloadProgressChanged(int value) =>
        OnPropertyChanged(nameof(DownloadingBannerText));

    private async Task CheckAsync() =>
        await _orchestrator.RunCheckAsync(AutoCheckEnabled);

    [RelayCommand]
    private void Restart() => _orchestrator.ApplyAndRestart();

    [RelayCommand]
    private void Retry() => _orchestrator.Retry();

    [RelayCommand]
    private void OpenReleaseNotes() =>
        _processService.Start(ReleaseNotesUrl, string.Empty, useShellExecute: true);
}
