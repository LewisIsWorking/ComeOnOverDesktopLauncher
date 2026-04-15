using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.Core.Services;

/// <summary>
/// Launches the ComeOnOver application.
/// Currently opens the web app in the default browser.
/// TODO: Detect and launch native desktop app when available.
/// </summary>
public class ComeOnOverAppService : IComeOnOverAppService
{
    private readonly IProcessService _processService;
    private readonly AppSettings _settings;

    public ComeOnOverAppService(IProcessService processService, AppSettings settings)
    {
        _processService = processService;
        _settings = settings;
    }

    public void Launch() =>
        _processService.Start(_settings.ComeOnOverUrl, useShellExecute: true);

    // TODO: Implement desktop app detection when ComeOnOver native app is available.
    public bool IsInstalled() => false;
}
