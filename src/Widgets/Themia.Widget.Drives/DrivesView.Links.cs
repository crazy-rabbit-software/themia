using Microsoft.UI.Xaml;
using Themia.Core.Links;
using Themia.Sdk.Devices;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11950] The drives list's place on its view's links: an item is its drive (<see cref="LinkKeys.DriveLink"/> of its
/// root). The pointer on it, or the keyboard cursor, publishes the drive (<see cref="LinkTopics.HoveredDrive"/>); an item
/// that is the drive pointed at anywhere on the view takes the item layer's hover fill.
/// </summary>
internal sealed partial class DrivesView
{
    private readonly WidgetLinks _links = new();
    private LinkLights<DriveLink>? _drives;

    private LinkLights<DriveLink> Drives => _drives ??= new LinkLights<DriveLink>(_links, LinkTopics.HoveredDrive);

    /// <summary>An item the list drew: its drive (the item's id is its root); the face with nothing to show is none.</summary>
    private void LinkItem(DeviceItem item, FrameworkElement view)
    {
        if (item.Id.Length > 0)
        {
            Drives.Row(view, LinkKeys.DriveLink(item.Id));
        }
    }

    /// <summary>The list draws its items again: the old ones are forgotten.</summary>
    private void ForgetItems() => Drives.Clear();

    /// <summary>[S-3307] The keyboard cursor publishes its item's drive as the hover; none when it lets go.</summary>
    private void PointCursor(DeviceItem? item) => Drives.Point(item is { Id.Length: > 0 } ? LinkKeys.DriveLink(item.Id) : null);
}
