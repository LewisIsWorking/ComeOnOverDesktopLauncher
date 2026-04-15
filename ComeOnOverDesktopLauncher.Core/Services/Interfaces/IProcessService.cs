namespace ComeOnOverDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Abstracts process operations to allow unit testing without spawning real processes.
/// </summary>
public interface IProcessService
{
    void Start(string fileName, string? arguments = null, bool useShellExecute = false);
    int CountByName(string processName);
}
