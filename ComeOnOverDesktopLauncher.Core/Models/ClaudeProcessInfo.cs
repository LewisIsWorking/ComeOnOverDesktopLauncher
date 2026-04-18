namespace ComeOnOverDesktopLauncher.Core.Models;

/// <summary>
/// Raw snapshot of a single running claude.exe process as observed via WMI.
/// Contains just enough to classify the process (<see cref="CommandLine"/>)
/// and correlate it with a resource-usage snapshot (<see cref="ProcessId"/>).
///
/// This is the <b>unclassified</b> form; see
/// <see cref="Services.Interfaces.IClaudeProcessClassifier"/> for conversion
/// to <see cref="SlotProcessInfo"/> or <see cref="ExternalProcessInfo"/>.
/// </summary>
public record ClaudeProcessInfo(int ProcessId, string CommandLine, DateTime StartTime);