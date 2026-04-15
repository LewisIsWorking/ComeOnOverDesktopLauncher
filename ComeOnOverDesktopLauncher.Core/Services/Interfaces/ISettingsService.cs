using ComeOnOverDesktopLauncher.Core.Models;

namespace ComeOnOverDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Loads and persists user preferences.
/// </summary>
public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}
