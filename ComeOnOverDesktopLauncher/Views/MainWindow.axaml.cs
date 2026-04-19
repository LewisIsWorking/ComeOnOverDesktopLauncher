using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ComeOnOverDesktopLauncher.Services.Interfaces;
using ComeOnOverDesktopLauncher.ViewModels;
using ComeOnOverDesktopLauncher.Views.Controls;

namespace ComeOnOverDesktopLauncher.Views;

public partial class MainWindow : Window
{
    /// <summary>
    /// Injected by App.axaml.cs immediately after construction so the
    /// Copy Screenshot button can capture this window without the VM
    /// needing an Avalonia Window reference.
    /// </summary>
    public IWindowSnapshotService? SnapshotService { get; set; }

    public MainWindow()
    {
        InitializeComponent();

        // The Copy screenshot button lives inside the ResourceTotalsRow
        // UserControl. The UserControl doesn't own the Window reference
        // needed to capture it, so it raises a CopyClicked event we
        // subscribe to here. Split out from the inline Click=... handler
        // in v1.8.2 when MainWindow.axaml was decomposed into UserControls.
        var totalsRow = this.FindControl<ResourceTotalsRow>("TotalsRow");
        if (totalsRow is not null)
            totalsRow.CopyClicked += OnCopyScreenshotClick;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Hide to tray instead of closing - use the tray icon Quit to fully exit
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

    private async void OnCopyScreenshotClick(object? sender, RoutedEventArgs e)
    {
        if (SnapshotService is null) return;

        var ok = await SnapshotService.CaptureAndCopyAsync(this);
        if (DataContext is MainWindowViewModel vm)
        {
            vm.StatusMessage = ok
                ? "Screenshot copied to clipboard"
                : "Screenshot failed - check logs";
        }
    }
}
