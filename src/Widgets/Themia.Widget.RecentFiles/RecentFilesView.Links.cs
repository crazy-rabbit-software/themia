using Microsoft.UI.Xaml;
using Themia.Core.Links;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11945] [S-11948] Recent files' place on its view's links: a row is its file (<see cref="LinkKeys.FileLink"/> of
/// its full path). The pointer on it, or the keyboard cursor, publishes it (<see cref="LinkTopics.HoveredFile"/>); a row
/// that is the file pointed at anywhere on the view takes the item layer's hover fill, lit in place on the rows the
/// kit's list drew.
/// </summary>
internal sealed partial class RecentFilesView
{
    private readonly WidgetLinks _links = new();
    private LinkLights<FileLink>? _files;

    private LinkLights<FileLink> Files => _files ??= new LinkLights<FileLink>(_links, LinkTopics.HoveredFile);

    /// <summary>A row the kit's list drew: its file.</summary>
    private void LinkRow(KitItem item, FrameworkElement row)
        => Files.Row(row, _source.FileOf(item.Id) is { } file ? LinkKeys.FileLink(file.Path) : null);

    /// <summary>The kit's list draws its rows again: the old ones are forgotten.</summary>
    private void ForgetRows() => Files.Clear();

    /// <summary>[S-3307] The keyboard cursor publishes its row's file as the hover; none when it lets go.</summary>
    private void PointCursor(KitItem? item)
        => Files.Point(item is not null && _source.FileOf(item.Id) is { } file ? LinkKeys.FileLink(file.Path) : null);
}
