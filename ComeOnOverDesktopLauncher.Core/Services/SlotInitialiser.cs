using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.Core.Services;

/// <summary>
/// Seeds a Claude slot with login credentials on first use.
/// Primary source: the default Claude profile at %APPDATA%\Claude.
/// Fallback: any other already-seeded slot (avoids locked-file failures when Claude is running).
/// </summary>
public class SlotInitialiser : ISlotInitialiser
{
    /// <summary>
    /// A fresh Chromium cookies SQLite database is 20480 bytes.
    /// Anything above this threshold means real login cookies are stored.
    /// </summary>
    private const long MinimalCookiesSizeBytes = 20480;
    private const int MaxFallbackSlots = 10;

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

        var networkDir = GetSlotNetworkDir(slot);
        _fileSystem.CreateDirectory(networkDir);

        if (TryCopyFrom(DefaultCookiesPath, GetSlotCookiesPath(slot))) return;

        // Fallback: copy from any already-seeded slot (avoids locked file when Claude is open)
        for (var i = 1; i <= MaxFallbackSlots; i++)
        {
            if (i == slot.SlotNumber) continue;
            var fallbackSlot = new LaunchSlot(i);
            if (!IsSeeded(fallbackSlot)) continue;
            if (TryCopyFrom(GetSlotCookiesPath(fallbackSlot), GetSlotCookiesPath(slot))) return;
        }
    }

    private bool TryCopyFrom(string sourcePath, string destinationPath)
    {
        if (!_fileSystem.FileExists(sourcePath)) return false;
        try
        {
            _fileSystem.CopyFile(sourcePath, destinationPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static string GetSlotNetworkDir(LaunchSlot slot) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            slot.DataDirectoryName, "Network");

    private static string GetSlotCookiesPath(LaunchSlot slot) =>
        Path.Combine(GetSlotNetworkDir(slot), "Cookies");
}
