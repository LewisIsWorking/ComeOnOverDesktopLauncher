using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.ViewModels;

/// <summary>
/// Stateless helper that coordinates thumbnail capture across a set of
/// <see cref="ClaudeInstanceViewModel"/> rows. Extracted from
/// <see cref="MainWindowViewModel"/> to keep that file under the
/// 200-line limit and to concentrate "capture one batch of thumbnails"
/// logic in one testable place.
///
/// <para>
/// Only visible (windowed) slots are refreshed. Tray-resident slots
/// intentionally keep whatever <see cref="ClaudeInstanceViewModel.Thumbnail"/>
/// they already have - the "frozen thumbnail" behaviour that lets
/// users still recognise a close-to-tray'd slot by its last-known
/// frame. Callers are expected to pass only the visible collection
/// (e.g. <c>SlotInstances.Items</c>, not <c>SlotInstances.TrayItems</c>).
/// </para>
/// </summary>
public static class ThumbnailRefresher
{
    /// <summary>
    /// Captures a fresh thumbnail for each row in <paramref name="visible"/>
    /// and pushes the bytes into the row's
    /// <see cref="ClaudeInstanceViewModel.UpdateThumbnailFromBytes"/>
    /// method. Service failures return null bytes and are no-oped by
    /// the VM, so a transient capture miss (e.g. GDI pressure) simply
    /// leaves the previous thumbnail in place.
    /// </summary>
    public static void RefreshVisibleThumbnails(
        IWindowThumbnailService service,
        IEnumerable<ClaudeInstanceViewModel> visible,
        int width,
        int height)
    {
        foreach (var vm in visible)
        {
            var bytes = service.CapturePngBytes(vm.ProcessId, width, height);
            vm.UpdateThumbnailFromBytes(bytes);
        }
    }

    /// <summary>
    /// Explicitly blanks every row's thumbnail across all supplied
    /// collections. Used when the user toggles the "Show thumbnails"
    /// setting off - we don't want stale captures sitting in memory
    /// after the feature has been disabled.
    /// </summary>
    public static void ClearAllThumbnails(
        params IEnumerable<ClaudeInstanceViewModel>[] collections)
    {
        foreach (var collection in collections)
            foreach (var vm in collection)
                vm.ClearThumbnail();
    }

    /// <summary>
    /// Handles the toggle-change side effect for the "Show thumbnails"
    /// setting: persists the new value via <paramref name="saveSettings"/>
    /// and, when disabling, immediately clears every existing thumbnail
    /// via <see cref="ClearAllThumbnails"/> so the next render cycle
    /// blanks rather than showing stale captures. Re-enabling does not
    /// force an immediate capture; the next poll tick will populate
    /// thumbnails normally.
    ///
    /// <para>
    /// In v1.9.0 only slot collections are cleared. External instances
    /// don't yet participate in thumbnail capture - they'll join in
    /// v1.9.1 when the grid card UI lands.
    /// </para>
    /// </summary>
    public static void HandleToggleChange(
        bool enabled,
        Core.Models.AppSettings settings,
        Action saveSettings,
        IEnumerable<ClaudeInstanceViewModel> items,
        IEnumerable<ClaudeInstanceViewModel> trayItems)
    {
        settings.ThumbnailsEnabled = enabled;
        saveSettings();
        if (!enabled)
            ClearAllThumbnails(items, trayItems);
    }
}
