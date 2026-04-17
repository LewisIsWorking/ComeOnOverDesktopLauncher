using System.Text;
using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;

namespace ComeOnOverDesktopLauncher.Tests.Services;

public class SlotInitialiserTests
{
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private readonly ILoggingService _logger = Substitute.For<ILoggingService>();
    private SlotInitialiser CreateSut() => new(_fileSystem, _logger);
    private readonly LaunchSlot _slot = new(1);

    private static readonly byte[] ValidSqliteHeader =
        Encoding.ASCII.GetBytes("SQLite format 3\0");
    private static readonly byte[] InvalidHeader = new byte[16];

    private string SlotCookiesPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClaudeSlot1", "Network", "Cookies");

    private string DefaultCookiesPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Claude", "Network", "Cookies");

    [Fact]
    public void IsSeeded_WhenCookiesFileDoesNotExist_ReturnsFalse()
    {
        _fileSystem.FileExists(SlotCookiesPath).Returns(false);
        Assert.False(CreateSut().IsSeeded(_slot));
    }

    [Fact]
    public void IsSeeded_WhenCookiesFileIsMinimal_ReturnsFalse()
    {
        _fileSystem.FileExists(SlotCookiesPath).Returns(true);
        _fileSystem.GetFileSize(SlotCookiesPath).Returns(20480L);
        Assert.False(CreateSut().IsSeeded(_slot));
    }

    [Fact]
    public void IsSeeded_WhenCookiesFileHasRealData_ReturnsTrue()
    {
        _fileSystem.FileExists(SlotCookiesPath).Returns(true);
        _fileSystem.GetFileSize(SlotCookiesPath).Returns(36864L);
        Assert.True(CreateSut().IsSeeded(_slot));
    }

    [Fact]
    public void EnsureInitialised_WhenAlreadySeeded_DoesNotCopy()
    {
        _fileSystem.FileExists(SlotCookiesPath).Returns(true);
        _fileSystem.GetFileSize(SlotCookiesPath).Returns(36864L);

        CreateSut().EnsureInitialised(_slot);

        _fileSystem.DidNotReceive().CopyFileSharedRead(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void EnsureInitialised_WhenDefaultExistsAndCopyValid_CopiesFromDefault()
    {
        _fileSystem.FileExists(SlotCookiesPath).Returns(false);
        _fileSystem.FileExists(DefaultCookiesPath).Returns(true);
        _fileSystem.ReadFileHeader(SlotCookiesPath, 16).Returns(ValidSqliteHeader);

        CreateSut().EnsureInitialised(_slot);

        _fileSystem.Received(1).CopyFileSharedRead(DefaultCookiesPath, SlotCookiesPath);
    }

    [Fact]
    public void EnsureInitialised_WhenCopyProducesInvalidSqlite_DiscardsAndTriesFallback()
    {
        // Default copies but produces invalid bytes; slot 2 exists & is seeded; its copy is valid
        _fileSystem.FileExists(SlotCookiesPath).Returns(false);
        _fileSystem.FileExists(DefaultCookiesPath).Returns(true);

        var slot2Cookies = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClaudeSlot2", "Network", "Cookies");
        _fileSystem.FileExists(slot2Cookies).Returns(true);
        _fileSystem.GetFileSize(slot2Cookies).Returns(36864L);

        // First header check (after default copy) -> invalid; second (after fallback copy) -> valid
        _fileSystem.ReadFileHeader(SlotCookiesPath, 16)
            .Returns(InvalidHeader, ValidSqliteHeader);

        CreateSut().EnsureInitialised(_slot);

        _fileSystem.Received(1).CopyFileSharedRead(DefaultCookiesPath, SlotCookiesPath);
        _fileSystem.Received(1).CopyFileSharedRead(slot2Cookies, SlotCookiesPath);
        _fileSystem.Received(1).DeleteFile(SlotCookiesPath);
    }

    [Fact]
    public void EnsureInitialised_WhenDefaultLockedAndFallbackSeeded_CopiesFromFallback()
    {
        _fileSystem.FileExists(SlotCookiesPath).Returns(false);
        _fileSystem.FileExists(DefaultCookiesPath).Returns(true);
        _fileSystem.When(f => f.CopyFileSharedRead(DefaultCookiesPath, Arg.Any<string>()))
            .Throw(new IOException("File locked"));

        var slot2CookiesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClaudeSlot2", "Network", "Cookies");
        _fileSystem.FileExists(slot2CookiesPath).Returns(true);
        _fileSystem.GetFileSize(slot2CookiesPath).Returns(36864L);
        _fileSystem.ReadFileHeader(SlotCookiesPath, 16).Returns(ValidSqliteHeader);

        CreateSut().EnsureInitialised(_slot);

        _fileSystem.Received(1).CopyFileSharedRead(slot2CookiesPath, SlotCookiesPath);
    }

    [Fact]
    public void EnsureInitialised_WhenNoSourceExists_DoesNotCopy()
    {
        _fileSystem.FileExists(SlotCookiesPath).Returns(false);
        _fileSystem.FileExists(Arg.Any<string>()).Returns(false);

        CreateSut().EnsureInitialised(_slot);

        _fileSystem.DidNotReceive().CopyFileSharedRead(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void EnsureInitialised_WhenAllCopiesFail_DoesNotThrow()
    {
        _fileSystem.FileExists(SlotCookiesPath).Returns(false);
        _fileSystem.FileExists(Arg.Any<string>()).Returns(true);
        _fileSystem.GetFileSize(Arg.Any<string>()).Returns(20480L);
        _fileSystem.When(f => f.CopyFileSharedRead(Arg.Any<string>(), Arg.Any<string>()))
            .Throw(new IOException("File locked"));

        Assert.Null(Record.Exception(() => CreateSut().EnsureInitialised(_slot)));
    }

    [Fact]
    public void EnsureInitialised_CreatesNetworkDirectoryBeforeCopying()
    {
        _fileSystem.FileExists(SlotCookiesPath).Returns(false);
        _fileSystem.FileExists(DefaultCookiesPath).Returns(true);
        _fileSystem.ReadFileHeader(SlotCookiesPath, 16).Returns(ValidSqliteHeader);

        var networkDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClaudeSlot1", "Network");

        CreateSut().EnsureInitialised(_slot);

        _fileSystem.Received(1).CreateDirectory(networkDir);
    }

    [Fact]
    public void EnsureInitialised_WhenDeleteOfInvalidCopyThrows_ContinuesGracefully()
    {
        _fileSystem.FileExists(SlotCookiesPath).Returns(false);
        _fileSystem.FileExists(DefaultCookiesPath).Returns(true);
        _fileSystem.ReadFileHeader(SlotCookiesPath, 16).Returns(InvalidHeader);
        _fileSystem.When(f => f.DeleteFile(SlotCookiesPath))
            .Throw(new IOException("locked"));

        Assert.Null(Record.Exception(() => CreateSut().EnsureInitialised(_slot)));
    }
}