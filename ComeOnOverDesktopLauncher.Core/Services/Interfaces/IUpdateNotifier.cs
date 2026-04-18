namespace ComeOnOverDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Builds the user-facing "a new version is available" message by combining
/// a release-check source with the running version. Separated from the raw
/// <see cref="IUpdateChecker"/> so the ViewModel only has to deal with the
/// finished UI string, not version-comparison plumbing.
/// </summary>
public interface IUpdateNotifier
{
    /// <summary>
    /// Returns a message suitable for display in the launcher UI (e.g.
    /// <c>"v1.8.0 available at github.com/LewisIsWorking/ComeOnOverDesktopLauncher"</c>),
    /// or <c>null</c> when no update is available or the check fails.
    /// Never throws - network/API errors collapse to <c>null</c> so the
    /// caller can simply hide the update banner.
    /// </summary>
    Task<string?> GetUpdateAvailableMessageAsync();
}