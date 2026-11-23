using Themia.Sdk.Items;
using Themia.Sdk.WinUI.Items;
using Microsoft.UI.Xaml;
using Themia.Core.Links;
using Themia.Core.Widgets;
using Themia.Sdk.Devices;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11955] [S-11958] Bluetooth's place on its view's links: an item is its device by the address its endpoint id
/// carries (<see cref="LinkKeys.DeviceOfEndpoint"/>). The pointer on it publishes the device
/// (<see cref="LinkTopics.HoveredDevice"/>); an item that is the device pointed at anywhere on the view takes the item
/// layer's hover fill.
/// </summary>
internal sealed partial class BluetoothView : IWidgetSurfaceAware
{
    private readonly WidgetLinks _links = new();
    private LinkLights<DeviceLink>? _devices;
    private IWidgetSurface? _surface;

    private LinkLights<DeviceLink> Devices => _devices ??= new LinkLights<DeviceLink>(_links, LinkTopics.HoveredDevice);

    public void Bind(IWidgetSurface surface) => _surface = surface;

    /// <summary>An item the list drew: its device (the item's id is its endpoint id); the face of an empty list is none.</summary>
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
