using System.Diagnostics;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.Core.Services;

/// <summary>
/// Resolves the Claude Desktop MSIX executable path on Windows.
/// Tries direct filesystem access first, falls back to PowerShell query.
/// </summary>
public class ClaudePathResolver : IClaudePathResolver
{
    private const string WindowsAppsPath = @"C:\Program Files\WindowsApps";
    private const string ClaudeExeRelativePath = @"app\claude.exe";
    private const string ClaudePackagePattern = "Claude_*";

    private readonly IFileSystem _fileSystem;

    public ClaudePathResolver(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public string? ResolveClaudeExePath() =>
        TryResolveDirectly() ?? ResolveViaPowerShell();

    public bool IsClaudeInstalled() => ResolveClaudeExePath() is not null;

    private string? TryResolveDirectly()
    {
        try
        {
            var dirs = _fileSystem.GetDirectories(WindowsAppsPath, ClaudePackagePattern);
            return dirs
                .Select(dir => Path.Combine(dir, ClaudeExeRelativePath))
                .FirstOrDefault(_fileSystem.FileExists);
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private string? ResolveViaPowerShell()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = "-Command \"(Get-AppxPackage | Where-Object { $_.Name -like '*Claude*' }).InstallLocation\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process is null) return null;

        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();

        if (string.IsNullOrWhiteSpace(output)) return null;

        var exePath = Path.Combine(output, "app", "claude.exe");
        return _fileSystem.FileExists(exePath) ? exePath : null;
    }
}
