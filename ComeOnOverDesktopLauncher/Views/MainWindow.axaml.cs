using Avalonia.Controls;

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
}
