#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): a widget holding 3, 7 or 20 shortcuts, in the grid (<c>grid3</c>,
/// <c>grid7</c>, <c>grid20</c>), the list (<c>list7</c>) or the details view (<c>details7</c>), so a
/// render shows where the items sit in the tile. Local locations only: no favicon is fetched.
/// "selected" is seven items in the render's style with two selected, one under the pointer and the
/// focus moved on by Ctrl+arrows ([S-6627]); "inactive" the same after the widget lost the keyboard
/// ([S-6626]); "lasso" a rubber band over the first items ([S-6624]); "fullname" a long name selected,
/// shown in full ([S-6629]).
/// The fixture is applied, never saved.
/// </summary>
internal sealed partial class ShortcutsView : IWidgetPreviewState
{
    private static readonly string[] PreviewShellNames =
    [
        "RecycleBinFolder", "MyComputerFolder", "Downloads", "Personal", "Desktop", "My Pictures",
        "My Music", "My Video", "UsersFilesFolder", "ControlPanelFolder", "NetworkPlacesFolder", "Profile",
    ];

    private static readonly string[] PreviewPaths =
    [
        @"C:\Windows", @"C:\Program Files", @"C:\Users", @"C:\Windows\System32",
        @"C:\Windows\notepad.exe", @"C:\Windows\explorer.exe", @"C:\ProgramData", @"C:\Windows\Fonts",
    ];

    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "grid3", "grid7", "grid20", "list7", "details7", "selected", "inactive", "lasso", "fullname"];

    public Task ShowPreviewStateAsync(string state)
    {
        // [S-6006] "sample": seven items in whatever style the render chose (--style).
        var (view, count) = state switch
        {
            "sample" or "selected" or "inactive" or "lasso" or "fullname" => (_config.Style, 7),
            "grid3" => (ShortcutsStyle.Grid, 3),
            "grid7" => (ShortcutsStyle.Grid, 7),
            "grid20" => (ShortcutsStyle.Grid, 20),
            "list7" => (ShortcutsStyle.List, 7),
            "details7" => (ShortcutsStyle.Details, 7),
            _ => (ShortcutsStyle.Grid, 0),
        };

        if (count == 0)
        {
            return Task.CompletedTask;
        }

        var targets = PreviewShellNames.Select(LaunchTargets.ShellTarget).Concat(PreviewPaths).Take(count);
        var items = targets.Select(target => ShortcutItem.Create(target)).ToList();

        if (state == "fullname")
        {
            items[1].Label = "Quarterly planning documents and meeting notes";
        }

        ApplyConfig(new ShortcutsConfig { Style = view, Items = items });
        UpdateLayout();

        switch (state)
        {
            case "selected" or "inactive":
                // The calls a click, a Ctrl+click, a pointer over a third and Ctrl+arrows make.
                _active = true;
                _selection.Click(0, ctrl: false, shift: false);
                _selection.Click(1, ctrl: true, shift: false);
                HandleKey(WidgetKey.FocusRight);
                HandleKey(WidgetKey.FocusDown);
                _cells[3].Hovered = true;

                if (state == "inactive")
                {
                    HandleKey(WidgetKey.Deactivate);
                }

                PaintAll();
                break;

            case "lasso" when _cells.Count >= 2:
                // From the empty space past the last item back over the first two, as a drag draws it.
                _active = true;
                var first = _cells[0].Root.TransformToVisual(_surface).TransformBounds(new Windows.Foundation.Rect(0, 0, _cells[0].Root.ActualWidth, _cells[0].Root.ActualHeight));
                var second = _cells[1].Root.TransformToVisual(_surface).TransformBounds(new Windows.Foundation.Rect(0, 0, _cells[1].Root.ActualWidth, _cells[1].Root.ActualHeight));
                _selection.BeginBand(BandMode.Replace);
                _band.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                DrawBand(new Windows.Foundation.Point(first.X + (first.Width / 3), first.Y + (first.Height / 2)),
                    new Windows.Foundation.Point(second.X + (second.Width * 2 / 3), _surface.ActualHeight - 4));
                break;

            case "fullname":
                _active = true;
                HandleKey(WidgetKey.Right);
                HandleKey(WidgetKey.Right);
                break;
        }

        return Task.CompletedTask;
    }
}
#endif
