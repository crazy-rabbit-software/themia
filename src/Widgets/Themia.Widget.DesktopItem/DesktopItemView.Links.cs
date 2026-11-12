using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Sdk;

namespace Themia.Widget.DesktopItem;

/// <summary>
/// [S-11945] [S-11949] The desktop item's place on its view's links: the item is its file, folder or link by
/// its path (<see cref="LinkKeys.LocalFileLink"/>; This PC, the Recycle Bin and every other shell item are none).
/// The pointer on it publishes it (<see cref="LinkTopics.HoveredFile"/>); while it is pointed at elsewhere on the
/// view the whole item is drawn as under the pointer (the desktop's hover), over what its own selection draws,
/// and by that selection alone again when it goes.
/// </summary>
internal sealed partial class DesktopItemView
{
    private readonly WidgetLinks _links = new();

    /// <summary>The file another widget of the view points at, or null.</summary>
    private FileLink? _pointedFile;

    /// <summary>[S-11945] Whether the item is the file pointed at elsewhere on the view.</summary>
    private bool PointedElsewhere => _pointedFile is not null && _pointedFile.Equals(FileOf(Item));

    /// <summary>Follow the files pointed at (once, from the constructor; heard again on every join).</summary>
    private void FollowFiles() => _links.Follow(LinkTopics.HoveredFile, file =>
    {
        _pointedFile = file;
        Paint();
    });

    /// <summary>The pointer came onto the item (<paramref name="on"/>) or left it: its file is the view's hover while it is there.</summary>
    private void PointItem(bool on) => _links.Set(LinkTopics.HoveredFile, on ? FileOf(Item) : null);

    private static FileLink? FileOf(Themia.Sdk.DesktopItem? item)
        => item is { Kind: not DesktopItemKind.ShellItem } ? LinkKeys.LocalFileLink(item.Key) : null;
}
