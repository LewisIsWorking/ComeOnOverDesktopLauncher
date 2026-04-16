using System.Reflection;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.Core.Services;

/// <summary>
/// Reads the application version from the executing assembly.
/// Automatically reflects the version set in the .csproj file.
/// </summary>
public class AssemblyVersionService : IVersionService
{
    public string Version { get; } = Assembly
        .GetEntryAssembly()?
        .GetName()
        .Version?
        .ToString(3) ?? "0.0.0";
}
