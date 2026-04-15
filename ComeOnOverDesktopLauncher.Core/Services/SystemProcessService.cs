using System.Diagnostics;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.Core.Services;

/// <summary>
/// Production process implementation using System.Diagnostics.Process.
/// </summary>
public class SystemProcessService : IProcessService
{
    public void Start(string fileName, string? arguments = null, bool useShellExecute = false)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments ?? string.Empty,
            UseShellExecute = useShellExecute
        });
    }

    public int CountByName(string processName) =>
        Process.GetProcessesByName(processName).Length;

    public int CountByNameWithWindow(string processName) =>
        Process.GetProcessesByName(processName)
            .Count(p => p.MainWindowHandle != IntPtr.Zero);
}
