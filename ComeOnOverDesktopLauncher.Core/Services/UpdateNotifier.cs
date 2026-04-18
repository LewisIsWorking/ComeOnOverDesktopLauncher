using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.Core.Services;

/// <summary>
/// Produces the UI-ready "update available" message by asking
/// <see cref="IUpdateChecker"/> for the latest released version and
/// comparing it against the running launcher version.
///
/// Any failure (network error, API rate-limit, malformed version string)
/// collapses to <c>null</c> so the caller can simply hide the banner
/// rather than having to handle exceptions inline.
/// </summary>
public class UpdateNotifier : IUpdateNotifier
{
    private const string ReleaseUrl =
        "github.com/LewisIsWorking/ComeOnOverDesktopLauncher";

    private readonly IUpdateChecker _updateChecker;
    private readonly IVersionProvider _versionProvider;

    public UpdateNotifier(
        IUpdateChecker updateChecker,
        IVersionProvider versionProvider)
    {
        _updateChecker = updateChecker;
        _versionProvider = versionProvider;
    }

    public async Task<string?> GetUpdateAvailableMessageAsync()
    {
        var latest = await _updateChecker.GetLatestVersionAsync();
        if (latest is null) return null;

        var current = _versionProvider.GetVersion();
        return _updateChecker.IsNewerVersion(current, latest)
            ? $"v{latest} available at {ReleaseUrl}"
            : null;
    }
}