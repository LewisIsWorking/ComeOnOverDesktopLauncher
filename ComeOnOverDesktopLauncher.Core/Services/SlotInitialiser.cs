using System.Text;
using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.Core.Services;

/// <summary>
/// Seeds a Claude slot with login credentials on first use.
/// Primary source: the default Claude profile at %APPDATA%\Claude.
/// Fallback: any other already-seeded slot.
///
/// Chromium keeps the cookies SQLite file open while Claude is running, so copies
/// are attempted with <see cref="IFileSystem.CopyFileSharedRead"/> (FileShare.ReadWrite).
/// A mid-flush copy can still be corrupt; we verify the result starts with the
/// SQLite magic header ("SQLite format 3\0") and discard it otherwise so a bad
/// copy never prevents a later attempt from succeeding.
///
/// Seeding decisions are logged so login-persistence failures can be traced.
/// </summary>
public class SlotInitialiser : ISlotInitialiser
{
    /// <summary>
    /// A fresh Chromium cookies SQLite database is 20480 bytes.
    /// Anything above this threshold means real login cookies are stored.
    /// </summary>
    private const long MinimalCookiesSizeBytes = 20480;
    private const int MaxFallbackSlots = 10;

    /// <summary>
    /// SQLite 3 database file magic header. Every valid SQLite DB begins with
    /// exactly these 16 bytes. Used to reject partial/corrupt copies.
    /// See https://www.sqlite.org/fileformat.html#magic_header_string
    /// </summary>
    private static readonly byte[] SqliteMagic =
        Encoding.ASCII.GetBytes("SQLite format 3\0");

    private readonly IFileSystem _fileSystem;
    private readonly ILoggingService _logger;

    private static readonly string DefaultCookiesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Claude", "Network", "Cookies");

    public SlotInitialiser(IFileSystem fileSystem, ILoggingService logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public bool IsSeeded(LaunchSlot slot)
    {
        var cookiesPath = GetSlotCookiesPath(slot);
        if (!_fileSystem.FileExists(cookiesPath)) return false;
        return _fileSystem.GetFileSize(cookiesPath) > MinimalCookiesSizeBytes;
    }

    public void EnsureInitialised(LaunchSlot slot)
    {
        if (IsSeeded(slot))
        {
            _logger.LogDebug($"Slot {slot.SlotNumber} already seeded - skipping");
            return;
        }

        _logger.LogInfo($"Seeding slot {slot.SlotNumber}");
        var networkDir = GetSlotNetworkDir(slot);
        _fileSystem.CreateDirectory(networkDir);
        var destination = GetSlotCookiesPath(slot);

        if (TryCopyAndVerify(DefaultCookiesPath, destination))
        {
            _logger.LogInfo($"Slot {slot.SlotNumber} seeded from default Claude profile");
            return;
        }

        for (var i = 1; i <= MaxFallbackSlots; i++)
        {
            if (i == slot.SlotNumber) continue;
            var fallbackSlot = new LaunchSlot(i);
            if (!IsSeeded(fallbackSlot)) continue;
            if (TryCopyAndVerify(GetSlotCookiesPath(fallbackSlot), destination))
            {
                _logger.LogInfo($"Slot {slot.SlotNumber} seeded from fallback slot {i}");
                return;
            }
        }

        _logger.LogWarning($"Slot {slot.SlotNumber} could not be seeded - no valid source available");
    }

    /// <summary>
    /// Attempts to copy <paramref name="sourcePath"/> to <paramref name="destinationPath"/>
    /// using shared-read so locked source files can still be copied. Verifies the
    /// copy is a well-formed SQLite DB (correct magic header) before accepting it;
    /// removes the destination if verification fails so the next fallback is tried
    /// without a corrupt file left behind.
    /// </summary>
    private bool TryCopyAndVerify(string sourcePath, string destinationPath)
    {
        if (!_fileSystem.FileExists(sourcePath)) return false;
        try
        {
            _fileSystem.CopyFileSharedRead(sourcePath, destinationPath);
        }
        catch (IOException ex)
        {
            _logger.LogWarning($"Copy from {sourcePath} failed: {ex.Message}");
            return false;
        }

        if (IsValidSqliteFile(destinationPath)) return true;

        _logger.LogWarning(
            $"Copy from {sourcePath} produced an invalid SQLite file - discarding");
        try
        {
            _fileSystem.DeleteFile(destinationPath);
        }
        catch (IOException ex)
        {
            _logger.LogWarning($"Could not delete invalid copy at {destinationPath}: {ex.Message}");
        }
        return false;
    }

    private bool IsValidSqliteFile(string path)
    {
        var header = _fileSystem.ReadFileHeader(path, SqliteMagic.Length);
        if (header.Length != SqliteMagic.Length) return false;
        for (var i = 0; i < SqliteMagic.Length; i++)
            if (header[i] != SqliteMagic[i]) return false;
        return true;
    }

    private static string GetSlotNetworkDir(LaunchSlot slot) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            slot.DataDirectoryName, "Network");

    private static string GetSlotCookiesPath(LaunchSlot slot) =>
        Path.Combine(GetSlotNetworkDir(slot), "Cookies");
}