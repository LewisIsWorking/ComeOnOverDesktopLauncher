using Avalonia.Controls;

namespace ComeOnOverDesktopLauncher.Views.Controls;

/// <summary>
/// One card representing a tray-resident (close-to-tray'd) Claude slot.
/// Hosted by <see cref="TrayInstanceList"/>'s <c>WrapPanel</c>
/// <c>ItemsPanelTemplate</c>. DataContext is <c>ClaudeInstanceViewModel</c>.
///
/// <para>
/// Structurally mirrors <see cref="SlotCard"/> but with three key
/// differences: the thumbnail carries a "Hidden" badge overlay and
/// shows at reduced opacity (to communicate stale / frozen state);
/// the nickname is read-only italic (the user cannot usefully edit a
/// name they cannot see in the slot window); the bottom button reads
/// "Quit" instead of "X" because there is no longer a visible window
/// whose state needs protecting behind a confirm dialog.
/// </para>
/// </summary>
public partial class TrayCard : UserControl
{
    public TrayCard()
    {
        InitializeComponent();
    }
}
