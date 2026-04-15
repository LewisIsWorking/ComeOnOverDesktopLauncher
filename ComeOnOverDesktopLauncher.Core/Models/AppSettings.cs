namespace ComeOnOverDesktopLauncher.Core.Models;

/// <summary>
/// User preferences persisted to disk between sessions.
/// </summary>
public class AppSettings
{
    public int DefaultSlotCount { get; set; } = 3;
    public string ComeOnOverUrl { get; set; } = "https://comeonover.netlify.app";
}
