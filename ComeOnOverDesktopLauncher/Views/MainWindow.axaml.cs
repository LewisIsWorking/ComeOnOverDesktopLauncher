using Avalonia.Controls;
using Avalonia.Input;

namespace ComeOnOverDesktopLauncher.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Hide to tray instead of closing — use the tray icon Quit to fully exit
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        // Clear TextBox focus when clicking outside any TextBox
        if (e.Source is not TextBox)
            Focus();
    }
}
