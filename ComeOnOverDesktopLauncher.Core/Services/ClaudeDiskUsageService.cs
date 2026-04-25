using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.Core.Services;

/// <summary>
/// Computes the combined on-disk size of all <c>ClaudeSlot*</c>
/// directories under <c>%LOCALAPPDATA%</c> by walking the file tree
/// on a thread-pool thread.
///
/// <para>
/// Each slot stores a full Chromium/Electron profile: IndexedDB,
/// Cache, GPU cache, extensions, and session data. A 7-slot install
/// typically occupies 80-90 GB. The scan is slow (~5-10 s) so this
/// service is called once at startup and on demand — never on the
/// main refresh tick.
/// </para>
/// </summary>
public class ClaudeDiskUsageService : IClaudeDiskUsageService
{
    private readonly Func<string> _localAppDataResolver;

    /// <summary>Production constructor — resolves %LOCALAPPDATA% at call time.</summary>
    public ClaudeDiskUsageService()
        : this(() => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)) { }

    /// <summary>Testing seam — inject a custom root resolver.</summary>
    internal ClaudeDiskUsageService(Func<string> localAppDataResolver)
    {
        _localAppDataResolver = localAppDataResolver;
    }

    /// <inheritdoc/>
    public Task<double> GetTotalGbAsync() =>
        Task.Run(() =>
        {
            try
            {
                var root = _localAppDataResolver();
                var slotDirs = Directory.GetDirectories(root, "ClaudeSlot*",
                    SearchOption.TopDirectoryOnly);
                if (slotDirs.Length == 0) return 0.0;

                long totalBytes = 0;
                foreach (var dir in slotDirs)
                {
                    foreach (var file in Directory.EnumerateFiles(
                        dir, "*", SearchOption.AllDirectories))
                    {
                        try { totalBytes += new FileInfo(file).Length; }
                        catch { /* file deleted mid-scan — skip */ }
                    }
                }
                return Math.Round(totalBytes / 1_073_741_824.0, 1);
            }
            catch
            {
                return 0.0;
            }
        });
}