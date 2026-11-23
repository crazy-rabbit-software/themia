using Microsoft.UI.Xaml;
using Themia.Core.Links;
using Themia.Sdk.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11950] The removable drives' place on its view's links: a row is its drive (<see cref="LinkKeys.DriveLink"/>
/// of its root). The pointer on it, or the keyboard cursor, publishes the drive
/// (<see cref="LinkTopics.HoveredDrive"/>); a row that is the drive pointed at anywhere on the view takes the item
/// layer's hover fill, lit in place on the rows the kit's list drew.
/// </summary>
internal sealed partial class RemovableDrivesView
{
    private readonly WidgetLinks _links = new();
    private LinkLights<DriveLink>? _drives;

    private LinkLights<DriveLink> Drives => _drives ??= new LinkLights<DriveLink>(_links, LinkTopics.HoveredDrive);

    /// <summary>A row the kit's list drew: its drive (the item's id is its root).</summary>
    private void LinkRow(KitItem item, FrameworkElement row) => Drives.Row(row, LinkKeys.DriveLink(item.Id));

    /// <summary>The kit's list draws its rows again: the old ones are forgotten.</summary>
    private void ForgetRows() => Drives.Clear();

    /// <summary>[S-3307] The keyboard cursor publishes its row's drive as the hover; none when it lets go.</summary>
    private void PointCursor(KitItem? item) => Drives.Point(item is null ? null : LinkKeys.DriveLink(item.Id));
}
