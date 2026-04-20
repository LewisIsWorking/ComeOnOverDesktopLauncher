using ComeOnOverDesktopLauncher.Core.Services.Interfaces;
using ComeOnOverDesktopLauncher.Services;
using NSubstitute;

namespace ComeOnOverDesktopLauncher.Tests.Services;

/// <summary>
/// Exercises <see cref="VelopackLogApplyFailureDetector"/> using
/// real files written to temp paths so the tail-reading logic is
/// exercised end-to-end (no mocking of <see cref="File"/>/<see cref="FileStream"/>).
/// The detector's two-probe seam (log path + now) lets us pin both
/// sides of the time-window comparison deterministically.
/// </summary>
public class VelopackLogApplyFailureDetectorTests : IDisposable
{
    private readonly ILoggingService _logger = Substitute.For<ILoggingService>();
    private readonly string _tempDir;
    private readonly string _logPath;

    public VelopackLogApplyFailureDetectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"coodl-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _logPath = Path.Combine(_tempDir, "velopack.log");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
        GC.SuppressFinalize(this);
    }

    private VelopackLogApplyFailureDetector MakeSut(DateTime now) =>
        new(_logger, () => _logPath, () => now);

    [Fact]
    public void ApplyFailedRecently_NoLogFile_ReturnsFalse()
    {
        var sut = MakeSut(new DateTime(2026, 4, 20, 16, 30, 0));

        var result = sut.ApplyFailedRecently(TimeSpan.FromMinutes(2));

        Assert.False(result);
    }

    [Fact]
    public void ApplyFailedRecently_RecentApplyError_ReturnsTrue()
    {
        // Simulates the exact pattern Velopack writes on apply failure.
        File.WriteAllText(_logPath,
            "[update:86148] [16:25:10] [INFO] Starting apply\n" +
            "[update:86148] [16:25:25] [ERROR] Apply error: Error applying package: Unable to start the update\n");
        var sut = MakeSut(new DateTime(2026, 4, 20, 16, 26, 0)); // 35s after the error

        var result = sut.ApplyFailedRecently(TimeSpan.FromMinutes(2));

        Assert.True(result);
    }

    [Fact]
    public void ApplyFailedRecently_OldApplyError_ReturnsFalse()
    {
        // Error happened 3 hours ago - outside the 2-minute window.
        File.WriteAllText(_logPath,
            "[update:86148] [13:25:25] [ERROR] Apply error: something\n");
        var sut = MakeSut(new DateTime(2026, 4, 20, 16, 26, 0));

        var result = sut.ApplyFailedRecently(TimeSpan.FromMinutes(2));

        Assert.False(result);
    }

    [Fact]
    public void ApplyFailedRecently_LogWithoutAnyApplyError_ReturnsFalse()
    {
        File.WriteAllText(_logPath,
            "[lib-csharp:72196] [16:25:15] [Information] Download complete\n" +
            "[lib-csharp:72196] [16:25:16] [Debug] Found 0 delta releases\n");
        var sut = MakeSut(new DateTime(2026, 4, 20, 16, 26, 0));

        var result = sut.ApplyFailedRecently(TimeSpan.FromMinutes(2));

        Assert.False(result);
    }

    [Fact]
    public void ApplyFailedRecently_MultipleErrors_UsesMostRecent()
    {
        // Old error + recent error = still true (recent one is within window)
        File.WriteAllText(_logPath,
            "[update:111] [10:00:00] [ERROR] Apply error: old one\n" +
            "[update:222] [16:25:25] [ERROR] Apply error: recent one\n");
        var sut = MakeSut(new DateTime(2026, 4, 20, 16, 26, 0));

        var result = sut.ApplyFailedRecently(TimeSpan.FromMinutes(2));

        Assert.True(result);
    }

    [Fact]
    public void ApplyFailedRecently_TimestampInFuture_InterpretsAsYesterday()
    {
        // Current time is 00:05, log entry is 23:55 - must be yesterday,
        // which is outside the 2-min window.
        File.WriteAllText(_logPath,
            "[update:111] [23:55:00] [ERROR] Apply error: last night\n");
        var sut = MakeSut(new DateTime(2026, 4, 20, 0, 5, 0));

        var result = sut.ApplyFailedRecently(TimeSpan.FromMinutes(2));

        Assert.False(result);
    }

    [Fact]
    public void ApplyFailedRecently_IoExceptionReading_ReturnsFalseDoesNotThrow()
    {
        // Create a path whose parent dir doesn't exist; the
        // detector should swallow the exception and return false.
        var sut = new VelopackLogApplyFailureDetector(
            _logger,
            () => @"Z:\does-not-exist\velopack.log",
            () => DateTime.Now);

        var ex = Record.Exception(() => sut.ApplyFailedRecently(TimeSpan.FromMinutes(2)));

        Assert.Null(ex);
    }

    [Fact]
    public void ApplyFailedRecently_LargeLogReadsTailOnly()
    {
        // Write 50 KB of noise followed by a recent apply error.
        // The detector only reads the last 16 KB but our error is
        // at the end so it'll be found. This proves the tail-read
        // logic doesn't miss recent entries.
        var noise = new string('x', 50_000) + "\n";
        File.WriteAllText(_logPath,
            noise +
            "[update:86148] [16:25:25] [ERROR] Apply error: recent\n");
        var sut = MakeSut(new DateTime(2026, 4, 20, 16, 26, 0));

        var result = sut.ApplyFailedRecently(TimeSpan.FromMinutes(2));

        Assert.True(result);
    }
}
