using Avalonia.Controls;

namespace ComeOnOverDesktopLauncher.Views.Controls;

/// <summary>
/// One card representing a launcher-managed Claude slot in the grid.
/// Hosted by <see cref="SlotInstanceList"/>'s <c>WrapPanel</c>
/// <c>ItemsPanelTemplate</c>. DataContext is <c>ClaudeInstanceViewModel</c>.
///
/// <para>
/// Card contents (top-to-bottom): 240x150 thumbnail preview, slot pill
/// + login-status pill row, editable nickname, CPU/RAM/uptime stats
/// row + kill button. Width is fixed at 256px so the grid tiles
/// cleanly at 2 columns on default window widths and collapses to 1
/// column on narrow windows.
/// </para>
/// </summary>
public partial class SlotCard : UserControl
{
    public SlotCard()
    {
        InitializeComponent();
    }
}
