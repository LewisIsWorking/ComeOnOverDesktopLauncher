using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComeOnOverDesktopLauncher.Core.Models;

namespace ComeOnOverDesktopLauncher.ViewModels;

/// <summary>
/// Wraps a single Claude instance's live resource stats and user-defined name.
/// Exposes a Kill command so users can fully close an instance rather than letting
/// Claude minimize it to the system tray.
///
/// <para>
/// <see cref="Thumbnail"/> holds the most recent PNG snapshot of the
/// instance's window as an Avalonia <see cref="Bitmap"/>, refreshed by
/// <see cref="MainWindowViewModel.RefreshResources"/> on every poll
/// tick via <see cref="UpdateThumbnailFromBytes"/>. Passing null bytes
/// is a no-op by design so close-to-tray'd slots retain their last
/// captured frame (the "frozen thumbnail" behaviour). Callers that
/// genuinely want to blank the thumbnail use <see cref="ClearThumbnail"/>.
/// </para>
/// </summary>
public partial class ClaudeInstanceViewModel : ObservableObject, IThumbnailableViewModel
{
    private readonly Action<int, string>? _onNameChanged;
    private readonly Action<int>? _onKill;
    private readonly Action<ClaudeInstanceViewModel>? _onShowPreview;

    [ObservableProperty] private int _instanceNumber;
    [ObservableProperty] private int _processId;
    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private double _ramMb;
    [ObservableProperty] private string _uptimeDisplay = string.Empty;
    [ObservableProperty] private string _slotName = string.Empty;
    [ObservableProperty] private Bitmap? _thumbnail;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoginStatusText))]
    [NotifyPropertyChangedFor(nameof(LoginStatusBackground))]
    [NotifyPropertyChangedFor(nameof(LoginStatusForeground))]
    private bool _isSeeded;

    public string LoginStatusText => IsSeeded ? "Logged in" : "Not logged in";

    /// <summary>
    /// Pill background as an <see cref="IBrush"/> so XAML can bind
    /// directly to a <c>Border.Background</c> without relying on the
    /// legacy string-to-<c>Color</c> coercion that stopped working
    /// cleanly inside compiled-binding UserControl data templates in
    /// v1.9.1 (the pill was rendering transparent in the new card
    /// layout). <see cref="SolidColorBrush"/> is frozen at
    /// construction and safe to keep as a singleton per instance.
    /// </summary>
    public IBrush LoginStatusBackground => IsSeeded
        ? new SolidColorBrush(Color.Parse("#2E7D32"))
        : new SolidColorBrush(Color.Parse("#5D2F2F"));

    /// <summary>
    /// Pill foreground as an <see cref="IBrush"/>. Same rationale as
    /// <see cref="LoginStatusBackground"/> - bind as
    /// <c>TextBlock.Foreground</c> directly, no coercion needed.
    /// </summary>
    public IBrush LoginStatusForeground => IsSeeded
        ? new SolidColorBrush(Color.Parse("#81C784"))
        : new SolidColorBrush(Color.Parse("#EF9A9A"));

    public string LoginStatusTooltip => IsSeeded
        ? "Logged in"
        : "Not yet logged in - will log in on first launch";

    public ClaudeInstanceViewModel(
        int instanceNumber,
        string initialName,
        bool isSeeded,
        Action<int, string>? onNameChanged = null,
        Action<int>? onKill = null,
        Action<ClaudeInstanceViewModel>? onShowPreview = null)
    {
        _instanceNumber = instanceNumber;
        _slotName = initialName;
        _isSeeded = isSeeded;
        _onNameChanged = onNameChanged;
        _onKill = onKill;
        _onShowPreview = onShowPreview;
    }

    partial void OnSlotNameChanged(string value)
    {
        _onNameChanged?.Invoke(InstanceNumber, value);
    }

    [RelayCommand]
    private void Kill()
    {
        _onKill?.Invoke(ProcessId);
    }

    /// <summary>
    /// Opens the lightbox-style preview window for this slot's
    /// current <see cref="Thumbnail"/>. No-op if the callback isn't
    /// wired or the thumbnail hasn't been captured yet; the preview
    /// service also guards against a null bitmap at its own layer.
    /// </summary>
    [RelayCommand]
    private void ShowPreview()
    {
        _onShowPreview?.Invoke(this);
    }

    public void UpdateFrom(InstanceResourceSnapshot snapshot)
    {
        ProcessId = snapshot.ProcessId;
        CpuPercent = snapshot.CpuPercent;
        RamMb = snapshot.RamMb;
        UptimeDisplay = snapshot.UptimeDisplay;
    }

    /// <summary>
    /// Replaces <see cref="Thumbnail"/> with a new <c>Bitmap</c> decoded
    /// from the supplied PNG bytes. A null or empty array is a no-op,
    /// not a clear - tray-resident slots rely on this to keep their
    /// last captured frame visible after the window goes away. The
    /// previous bitmap (if any) is disposed to release its unmanaged
    /// buffer.
    /// </summary>
    public void UpdateThumbnailFromBytes(byte[]? pngBytes)
    {
        if (pngBytes is null || pngBytes.Length == 0) return;
        var old = Thumbnail;
        using var ms = new MemoryStream(pngBytes);
        Thumbnail = new Bitmap(ms);
        old?.Dispose();
    }

    /// <summary>
    /// Explicitly blanks the thumbnail and disposes its unmanaged
    /// buffer. Called when the user toggles the "Show thumbnails"
    /// setting off, or when a row is about to be removed from the
    /// collection entirely.
    /// </summary>
    public void ClearThumbnail()
    {
        var old = Thumbnail;
        Thumbnail = null;
        old?.Dispose();
    }
}
