using Themia.Core.Links;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11950] The removable drives' place on its view's links: a row is its drive (<see cref="LinkKeys.DriveLink"/>
/// of its root). The pointer on it, or the keyboard cursor, publishes the drive
/// (<see cref="LinkTopics.HoveredDrive"/>); a row that is the drive pointed at anywhere on the view takes the item
/// layer's hover fill, in place.
/// </summary>
internal sealed partial class RemovableDrivesView
{
    private readonly WidgetLinks _links = new();
    private LinkLights<DriveLink>? _drives;

    private LinkLights<DriveLink> Drives => _drives ??= new LinkLights<DriveLink>(_links, LinkTopics.HoveredDrive);

    /// <summary>The rows as they are shown: each visible row is its drive, a hidden one nothing.</summary>
    private void LinkRows()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            if (i < _devices.Count)
            {
                Drives.Row(_rows[i].Block, LinkKeys.DriveLink(_devices[i].Root));
            }
            else
            {
                Drives.Remove(_rows[i].Block);
            }
        }

        Drives.Apply();
    }

    /// <summary>[S-3307] The keyboard cursor publishes its row's drive as the hover; none when it lets go.</summary>
    private void PointCursor()
        => Drives.Point(_focus >= 0 && _focus < _devices.Count ? LinkKeys.DriveLink(_devices[_focus].Root) : null);
}
