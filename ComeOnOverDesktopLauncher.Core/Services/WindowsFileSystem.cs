using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.Core.Services;

/// <summary>
/// Production filesystem implementation using System.IO.
/// </summary>
public class WindowsFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public string[] GetDirectories(string path, string searchPattern) =>
        Directory.GetDirectories(path, searchPattern);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public void WriteAllText(string path, string content) =>
        File.WriteAllText(path, content);

    public void CreateDirectory(string path) =>
        Directory.CreateDirectory(path);
}
