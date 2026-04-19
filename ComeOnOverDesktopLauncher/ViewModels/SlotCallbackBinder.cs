using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.ViewModels;

/// <summary>
/// Stateless helper that wires the per-row callbacks on
/// <see cref="SlotInstanceListViewModel"/> back to the launcher/settings/
/// refresh orchestration in <see cref="MainWindowViewModel"/>. The slot
/// VM cannot do these itself because it doesn't own
/// <see cref="AppSettings"/>, <see cref="IClaudeInstanceLauncher"/>, or
/// the refresh mechanism - those are the main window's concerns. This
/// one-shot binding concentrates the adapter layer in a single method
/// so the calling constructor stays clean.
/// </summary>
public static class SlotCallbackBinder
{
    /// <summary>
    /// Wires every per-row callback on <paramref name="slots"/> to the
    /// supplied dependencies. Call once during MainWindowViewModel
    /// construction; the slot VM will invoke these as the user
    /// interacts with individual rows (edits a nickname, clicks the
    /// kill button).
    /// </summary>
    public static void Bind(
        SlotInstanceListViewModel slots,
        AppSettings settings,
        IClaudeInstanceLauncher launcher,
        Action saveSettings,
        Action refreshResources)
    {
        slots.GetSlotName = num => settings.GetSlotName(num);
        slots.OnSlotNameChanged = (slotNumber, name) =>
        {
            settings.SlotNames[slotNumber] = name;
            saveSettings();
        };
        slots.OnKillInstance = processId =>
        {
            launcher.KillInstance(processId);
            refreshResources();
        };
    }
}
