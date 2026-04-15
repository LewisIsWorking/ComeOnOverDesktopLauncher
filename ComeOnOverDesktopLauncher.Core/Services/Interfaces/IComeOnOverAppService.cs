namespace ComeOnOverDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Launches the ComeOnOver application.
/// Currently opens the web app; will support native app detection in future.
/// </summary>
public interface IComeOnOverAppService
{
    void Launch();
    bool IsInstalled();
}
