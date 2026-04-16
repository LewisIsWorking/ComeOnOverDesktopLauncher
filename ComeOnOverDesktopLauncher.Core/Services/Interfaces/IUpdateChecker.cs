namespace ComeOnOverDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Checks GitHub releases to see if a newer version of the launcher is available.
/// </summary>
public interface IUpdateChecker
{
    Task<string?> GetLatestVersionAsync();
    bool IsNewerVersion(string current, string latest);
}
