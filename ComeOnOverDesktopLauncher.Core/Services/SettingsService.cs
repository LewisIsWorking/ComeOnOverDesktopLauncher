using System.Text.Json;
using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.Core.Services;

/// <summary>
/// Persists user preferences to %APPDATA%\ComeOnOverDesktopLauncher\settings.json.
/// </summary>
public class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly IFileSystem _fileSystem;
    private readonly string _settingsPath;

    public SettingsService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ComeOnOverDesktopLauncher",
            "settings.json");
    }

    public AppSettings Load()
    {
        if (!_fileSystem.FileExists(_settingsPath))
            return new AppSettings();

        var json = _fileSystem.ReadAllText(_settingsPath);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        _fileSystem.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        _fileSystem.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
