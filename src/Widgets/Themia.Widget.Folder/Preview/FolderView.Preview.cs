#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-4122], [S-2056]): point the widget at the sample day's folders, written
/// under the temp folder (<see cref="SampleFiles"/>), so a render shows ordinary names, the shell's
/// stock icons and thumbnails, and never a name, path or picture of this PC. "documents" is the
/// folder's sample (<see cref="ShowSampleAsync"/>), "pictures" the sample pictures (drawn in code), "lasso"
/// the documents with a band dragged from beside the first name down past the last row ([S-6412]),
/// "selected" the documents with two items selected, a third under the pointer and the focus moved
/// on by Ctrl+Right ([S-6627]), "inactive" the same selection after the widget lost the keyboard
/// ([S-6626]), and "fullname" a tile whose long name is selected and shown in full ([S-6629]).
/// </summary>
internal sealed partial class FolderView : IWidgetPreviewState, IWidgetPreviewMenus
{
    /// <summary>
    /// [S-1747] For <c>--menu-png</c>: the real item menus of the first file in the pictures and of
    /// the first folder in the documents, the background menu, and the file's Open with submenu,
    /// built by the same code a right-click runs.
    /// </summary>
    public async Task<IReadOnlyList<(string Name, IReadOnlyList<ItemMenuEntry> Entries)>> PreviewMenusAsync()
    {
        await ShowPreviewStateAsync("pictures").ConfigureAwait(true);
        var file = MenuFor(_shown.FirstOrDefault(e => !e.IsDirectory));
        var openWith = _shown.FirstOrDefault(e => !e.IsDirectory) is { FullPath: { } path } ? OpenWithEntries(path) : [];

        await ShowPreviewStateAsync("documents").ConfigureAwait(true);
        var folder = MenuFor(_shown.FirstOrDefault(e => e.IsDirectory));
        _selection.Clear();
        return [("file", file), ("openwith", openWith), ("folder", folder), ("background", BackgroundEntries())];
    }

    private List<ItemMenuEntry> MenuFor(FolderEntry entry)
    {
        if (entry.FullPath is null)
        {
            return [];
        }

        _selection.SelectOnly(entry.FullPath);
        return ItemEntries();
    }

    public IReadOnlyList<string> PreviewStates { get; } = ["documents", "pictures", "lasso", "selected", "inactive", "fullname"];

    public async Task ShowPreviewStateAsync(string state)
    {
        if (state == "documents")
        {
            await ShowSampleAsync().ConfigureAwait(true);
            return;
        }

        var path = state switch
        {
            "lasso" or "selected" or "inactive" or "fullname" => SampleFiles.Write("Documents", SampleFiles.Documents),
            "pictures" => await SamplePictureFiles.WriteAsync("Pictures", SamplePictures.Names).ConfigureAwait(true),
            _ => null,
        };

        if (path is null)
        {
            return;
        }

        await ShowFolderAsync(path).ConfigureAwait(true);

        if (state == "lasso")
        {
            // The same calls a real drag makes, from the empty part of the first row.
            var row = FolderLayout.RowHeight;
            var x = Math.Max(0, _items.ActualWidth - 140);
            _active = true;
            _selection.BeginBand(BandMode.Replace);
            _gesture.BeginPress(x, row / 2, FolderPress.ItemBlank);
            _gesture.Move(x + 90, row * 6);
            DrawBand(_gesture.BandRect);
        }

        if (state is "selected" or "inactive" && _visuals.Count >= 4)
        {
            // The calls a click, a Ctrl+click, a pointer over a third and Ctrl+Right make.
            _active = true;
            _selection.Click(0, ctrl: false, shift: false);
            _selection.Click(1, ctrl: true, shift: false);
            HandleKey(WidgetKey.FocusRight);
            HandleKey(WidgetKey.FocusDown);
            _visuals[3].Hovered = true;

            if (state == "inactive")
            {
                HandleKey(WidgetKey.Deactivate);
            }

            RefreshSelectionVisuals();

            // The focus in view as the keys left it, at once: the keys' scroll is animated, and a shot
            // taken while it runs would catch the list half way.
            // (the least scroll that shows the focused row whole, as BringIntoView would end up).
            if (_selection.FocusIndex is var focus && focus >= 0)
            {
                var (extent, columns) = ItemExtent();
                _scroll.UpdateLayout();
                var bottom = ((focus / columns) + 1) * extent;
                _scroll.ChangeView(null, Math.Max(0, bottom - _scroll.ViewportHeight), null, disableAnimation: true);
            }
        }

        if (state == "fullname" && _shown.Count > 0)
        {
            // The longest name, selected by type-ahead as a keyboard user would.
            var longest = _shown.Select((e, i) => (e.Name.Length, i)).Max().i;
            _active = true;
            _selection.Type(longest);
            RealizeThrough(longest);
            RefreshSelectionVisuals();
            BringIntoView(longest);
        }
    }
}
#endif
