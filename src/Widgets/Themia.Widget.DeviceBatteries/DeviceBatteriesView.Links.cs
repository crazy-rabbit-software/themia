using Microsoft.UI.Xaml;
using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Sdk.Items;
using Themia.Sdk.Devices;

namespace Themia.Widget.DeviceBatteries;

/// <summary>
/// [S-11955] [S-11958] The batteries' place on its view's links: a row is its device by the address its id
/// carries where Windows reported it through a Bluetooth endpoint (<see cref="LinkKeys.DeviceOfEndpoint"/>; a
/// device container's or a game controller's own reading names no address and takes no part). The pointer on it
/// publishes the device (<see cref="LinkTopics.HoveredDevice"/>); an item that is the device pointed at anywhere on
/// the view takes the item layer's hover fill, lit in place.
/// </summary>
internal sealed partial class DeviceBatteriesView
{
    private readonly WidgetLinks _links = new();
    private LinkLights<DeviceLink>? _devices;
    private IWidgetSurface? _surface;

    private LinkLights<DeviceLink> Devices => _devices ??= new LinkLights<DeviceLink>(_links, LinkTopics.HoveredDevice);

    /// <summary>An item the list drew: its device; the face with no battery to show is none.</summary>
    private void LinkItem(Item item, FrameworkElement view)
    {
        if (item.Id.Length > 0)
        {
            Devices.Row(view, LinkKeys.DeviceOfEndpoint(item.Id));
        }
    }

    /// <summary>The list draws its items again: the old ones are forgotten.</summary>
    private void ForgetItems() => Devices.Clear();
}
