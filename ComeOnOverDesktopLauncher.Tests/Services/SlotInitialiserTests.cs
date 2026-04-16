using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;

namespace ComeOnOverDesktopLauncher.Tests.Services;

public class SlotInitialiserTests
{
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private SlotInitialiser CreateSut() => new(_fileSystem);
    private readonly LaunchSlot _slot = new(1);

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

        _fileSystem.DidNotReceive().CopyFile(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void EnsureInitialised_WhenNotSeededAndDefaultExists_CopiesCookies()
    {
        _fileSystem.FileExists(SlotCookiesPath).Returns(false);
        _fileSystem.FileExists(DefaultCookiesPath).Returns(true);

        CreateSut().EnsureInitialised(_slot);

        _fileSystem.Received(1).CopyFile(DefaultCookiesPath, SlotCookiesPath);
    }

    [Fact]
    public void EnsureInitialised_WhenDefaultDoesNotExist_DoesNotCopy()
    {
        _fileSystem.FileExists(SlotCookiesPath).Returns(false);
        _fileSystem.FileExists(DefaultCookiesPath).Returns(false);

        CreateSut().EnsureInitialised(_slot);

        _fileSystem.DidNotReceive().CopyFile(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void EnsureInitialised_WhenCopyThrowsIOException_DoesNotThrow()
    {
        _fileSystem.FileExists(SlotCookiesPath).Returns(false);
        _fileSystem.FileExists(DefaultCookiesPath).Returns(true);
        _fileSystem.When(f => f.CopyFile(Arg.Any<string>(), Arg.Any<string>())).Throw(new IOException("File locked"));

        var ex = Record.Exception(() => CreateSut().EnsureInitialised(_slot));

        Assert.Null(ex);
    }

    [Fact]
    public void EnsureInitialised_CreatesNetworkDirectoryBeforeCopying()
    {
        _fileSystem.FileExists(SlotCookiesPath).Returns(false);
        _fileSystem.FileExists(DefaultCookiesPath).Returns(true);
        var networkDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClaudeSlot1", "Network");

        CreateSut().EnsureInitialised(_slot);

        _fileSystem.Received(1).CreateDirectory(networkDir);
    }
}

