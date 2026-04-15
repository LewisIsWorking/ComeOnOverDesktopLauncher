namespace ComeOnOverDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Abstracts filesystem operations to allow unit testing without real disk access.
/// </summary>
public interface IFileSystem
{
    bool FileExists(string path);
    string[] GetDirectories(string path, string searchPattern);
    string ReadAllText(string path);
    void WriteAllText(string path, string content);
    void CreateDirectory(string path);
}
