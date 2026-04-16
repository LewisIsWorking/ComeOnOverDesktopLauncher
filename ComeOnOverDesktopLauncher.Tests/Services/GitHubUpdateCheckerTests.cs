using ComeOnOverDesktopLauncher.Core.Services;

namespace ComeOnOverDesktopLauncher.Tests.Services;

public class GitHubUpdateCheckerTests
{
    private readonly GitHubUpdateChecker _sut = new(new System.Net.Http.HttpClient());

    [Fact]
    public void IsNewerVersion_WhenLatestIsHigher_ReturnsTrue()
    {
        Assert.True(_sut.IsNewerVersion("1.2.0", "1.3.0"));
    }

    [Fact]
    public void IsNewerVersion_WhenLatestIsLower_ReturnsFalse()
    {
        Assert.False(_sut.IsNewerVersion("1.4.0", "1.3.0"));
    }

    [Fact]
    public void IsNewerVersion_WhenSameVersion_ReturnsFalse()
    {
        Assert.False(_sut.IsNewerVersion("1.3.0", "1.3.0"));
    }

    [Fact]
    public void IsNewerVersion_WhenCurrentIsInvalid_ReturnsFalse()
    {
        Assert.False(_sut.IsNewerVersion("not-a-version", "1.3.0"));
    }

    [Fact]
    public void IsNewerVersion_WhenLatestIsInvalid_ReturnsFalse()
    {
        Assert.False(_sut.IsNewerVersion("1.2.0", "not-a-version"));
    }

    [Fact]
    public void IsNewerVersion_WhenPatchVersionIsHigher_ReturnsTrue()
    {
        Assert.True(_sut.IsNewerVersion("1.2.0", "1.2.1"));
    }

    [Fact]
    public void IsNewerVersion_WhenMajorVersionIsHigher_ReturnsTrue()
    {
        Assert.True(_sut.IsNewerVersion("1.9.9", "2.0.0"));
    }
}
