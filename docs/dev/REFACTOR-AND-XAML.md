# CoODL — Refactor & Avalonia gotchas

Extracted from `LEARNINGS.md` during v1.9.2 to keep the root file under the 200-line limit. These are lessons from refactoring this codebase and working with Avalonia 12.

## Refactor gotchas

### `using NSubstitute;` does NOT flow through test helpers

When splitting test files, the `Returns(...)` extension method lives in `NSubstitute`, not in xUnit or any base namespace. **Every new test file needs `using NSubstitute;`** if it calls `.Returns(...)`, `.Received()`, `.When(...)` etc. Cost of forgetting: a build iteration per file. Copy the full `using` block verbatim when extracting test classes.

### Static helper methods on fixtures

When a test helper calls a static factory method on a fixture class (e.g. `MyFixture.Snap(pid)`), the test file needs to qualify it with the full class name unless imported. Helpers live on the fixture class, not as free statics, so they're always called `FixtureName.Helper(args)`. It reads ugly but beats duplicating across 15 test methods.

### Avalonia compiled-binding XAML split

Splitting a large `Window.axaml` into `UserControl`s is safe ONLY if:

- Each `UserControl.axaml` has `x:DataType="vm:MainWindowViewModel"` (or whatever the parent's DataType is) so inherited bindings still compile.
- Each `UserControl.axaml.cs` has `partial class` + `InitializeComponent()` call.
- Every binding that used to reference the code-behind (e.g. `Click="OnCopyScreenshotClick"`) needs to be replaced with a `RoutedEvent` pattern: name the button with `x:Name="MyButton"`, expose an event on the UserControl, let MainWindow subscribe.
- Use `x:Name`, not `Name=` — Avalonia's compiled-XAML source generator requires the `x:` prefix.

### WMI scanner — Electron browser-main quirk

Chromium/Electron's main process (the one with the visible window) reports an **empty args list** to WMI. Its `--user-data-dir` flag only appears on child processes. If you filter by windowed + cmdline match, every Claude window mis-classifies as external.

Fix in `WmiClaudeProcessScanner`: query `ParentProcessId` too, walk each windowed main's direct children, and when the main's own cmdline lacks the flag, extract `--user-data-dir=` from any child and synthesise a minimal enriched cmdline. Uses `ExtractFlagValue` helper that handles both quoted and bare values.

### Close-to-tray process state

A Claude slot that's been close-to-tray'd has:

- Main process still alive (PID 76748 or whatever)
- `MainWindowHandle == 0` (window is hidden)
- ~12 child processes still alive under it

The windowed-only filter (`MainWindowHandle != 0`) correctly suppresses Electron children but also incorrectly suppresses tray-resident slot mains. Use parent-process identity instead: a claude.exe is a "main" if its parent is not also a claude.exe.

### Avalonia `Grid` SizeToContent constraint

Don't use `RowDefinitions="Auto,*,Auto"` inside a window with `SizeToContent`. Circular constraint — the `*` wants the whole space, but there's no fixed space. Use `Auto,Auto,Auto` and let content size itself.

### Clipboard bitmap capture

`Graphics.CopyFromScreen` captures screen coordinates, NOT the window's own content — if another window is on top, you capture that other window. Use Win32 `PrintWindow` with `PW_RENDERFULLCONTENT = 2` flag. Captures the window's own device context. Works even when the window is partially off-screen or covered.

### Avalonia 12 clipboard API

Use `RenderTargetBitmap.Render(visual)` + `ClipboardExtensions.SetBitmapAsync`, not GDI. The visual tree render works regardless of window state (maximised, minimised-then-restored, multi-monitor). Bitmap lands on the Windows clipboard in all formats simultaneously (`image/png`, `PNG`, `DeviceIndependentBitmap`, `Format17`, `Bitmap`) so it pastes into Slack/Discord/Word/Paint without fuss.

## Avalonia 12 XAML gotchas (v1.9.x series)

### Compiled bindings don't coerce `string` -> `Color` inside UserControl DataTemplates

The pattern that worked in v1.8.x row templates:

```xaml
<Border>
    <Border.Background>
        <SolidColorBrush Color="{Binding LoginStatusBackground}"/>
    </Border.Background>
    ...
</Border>
```

where `LoginStatusBackground` is a `string` like `#2E7D32` — **stops coercing** when the same markup lives inside a `<DataTemplate>` that's hosted by a UserControl in a WrapPanel grid. The binding attaches but the property becomes transparent. Compiled bindings + nested subelement + templated context = silent coercion failure.

**Fix:** return an `IBrush` from the VM and bind directly:

```csharp
public IBrush LoginStatusBackground => IsSeeded
    ? new SolidColorBrush(Color.Parse("#2E7D32"))
    : new SolidColorBrush(Color.Parse("#5D2F2F"));
```

```xaml
<Border Background="{Binding LoginStatusBackground}">...</Border>
```

No nested subelement, no type coercion, always works. ViewModels in the UI project are allowed to reference `Avalonia.Media` - only the Core project stays Avalonia-free.

### `Tapped` on `Border` inside a DataTemplate doesn't fire reliably; use `PointerPressed`

Avalonia 12's `Tapped` routed event has gesture-recognition prerequisites that don't always set up correctly when a `Border` is hosted inside a UserControl DataTemplate. The XAML `Tapped="OnHandler"` attribute attaches the handler but clicks don't fire it.

**Fix:** use `PointerPressed` — lower-level, fires unconditionally on pointer-down. Filter to left-button so right-click stays free for future context menus:

```csharp
private void OnThumbnailPressed(object? sender, PointerPressedEventArgs e)
{
    if (e.GetCurrentPoint(null).Properties.PointerUpdateKind
        != PointerUpdateKind.LeftButtonPressed) return;
    // ... invoke command
}
```

### Child `Image` can swallow pointer events before they reach the parent `Border`

When the card thumbnail is structured `<Border Cursor="Hand" PointerPressed="..."><Image.../></Border>`, the Image can consume the pointer event if its bounds cover the Border. Set `IsHitTestVisible="False"` on the Image so clicks pass through to the Border handler.

### Overlay badges on top of a hit-testable element need `IsHitTestVisible="False"`

Same story for the "Hidden" badge on `TrayCard`'s thumbnail. Grid layer on top of the clickable Border → set `IsHitTestVisible="False"` on the overlay so clicks reach the layer below.
