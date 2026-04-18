using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ComeOnOverDesktopLauncher.Services.Interfaces;
using ComeOnOverDesktopLauncher.ViewModels;

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