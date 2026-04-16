using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.Core.Services;

/// <summary>
/// Checks the GitHub Releases API for the latest version of the launcher.
/// Uses the LewisIsWorking/ComeOnOverDesktopLauncher repository.
/// Returns null silently on any network failure so the app still launches cleanly.
/// </summary>
public class GitHubUpdateChecker : IUpdateChecker
{
    private const string ReleasesApiUrl =
        "https://api.github.com/repos/LewisIsWorking/ComeOnOverDesktopLauncher/releases/latest";

    private readonly HttpClient _httpClient;

    public GitHubUpdateChecker(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string?> GetLatestVersionAsync()
    {
        try
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ComeOnOverDesktopLauncher");
            var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(ReleasesApiUrl);
            return release?.TagName?.TrimStart('v');
        }
        catch
        {
            // Network unavailable, rate limited, or GitHub down - fail silently
            return null;
        }
    }

    public bool IsNewerVersion(string current, string latest)
    {
        if (!Version.TryParse(current, out var currentVersion)) return false;
        if (!Version.TryParse(latest, out var latestVersion)) return false;
        return latestVersion > currentVersion;
    }

    private sealed record GitHubRelease([property: JsonPropertyName("tag_name")] string? TagName);
}
