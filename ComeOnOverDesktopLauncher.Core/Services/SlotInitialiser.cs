using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.Core.Services;

/// <summary>
/// Seeds a Claude slot with login credentials from the default Claude profile
/// on first use. The default Claude profile is at %APPDATA%\Claude.
/// Slots are considered unseeded when their Cookies file is absent or minimal.
/// </summary>
public class SlotInitialiser : ISlotInitialiser
{
    /// <summary>
    /// A fresh Chromium cookies SQLite database is 20480 bytes.
    /// Anything at or below this threshold means no real cookies are stored.
    /// </summary>
    private const long MinimalCookiesSizeBytes = 20480;

    private readonly IFileSystem _fileSystem;

    private static readonly string DefaultCookiesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Claude", "Network", "Cookies");

    public SlotInitialiser(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public bool IsSeeded(LaunchSlot slot)
    {
        var cookiesPath = GetSlotCookiesPath(slot);
        if (!_fileSystem.FileExists(cookiesPath)) return false;
        return _fileSystem.GetFileSize(cookiesPath) > MinimalCookiesSizeBytes;
    }

    public void EnsureInitialised(LaunchSlot slot)
    {
        if (IsSeeded(slot)) return;
        if (!_fileSystem.FileExists(DefaultCookiesPath)) return;

        try
        {
            var networkDir = GetSlotNetworkDir(slot);
            _fileSystem.CreateDirectory(networkDir);
            _fileSystem.CopyFile(DefaultCookiesPath, GetSlotCookiesPath(slot));
        }
        catch (IOException)
        {
            // Default profile may be locked if Claude is running.
            // Silently skip — user will need to log in once for this slot.
        }
    }

    private static string GetSlotNetworkDir(LaunchSlot slot) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            slot.DataDirectoryName, "Network");

    private static string GetSlotCookiesPath(LaunchSlot slot) =>
        Path.Combine(GetSlotNetworkDir(slot), "Cookies");
}
