using Microsoft.UI.Xaml;
using Themia.Core.Links;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11955] [S-11958] Bluetooth's place on its view's links: a row is its device by the address its endpoint id
/// carries (<see cref="LinkKeys.DeviceOfEndpoint"/>). The pointer on it publishes the device
/// (<see cref="LinkTopics.HoveredDevice"/>); a row that is the device pointed at anywhere on the view takes the item
/// layer's hover fill, lit in place on the rows the kit's list drew.
/// </summary>
internal sealed partial class BluetoothView : IWidgetSurfaceAware
{
    private readonly WidgetLinks _links = new();
    private LinkLights<DeviceLink>? _devices;
    private IWidgetSurface? _surface;

    private LinkLights<DeviceLink> Devices => _devices ??= new LinkLights<DeviceLink>(_links, LinkTopics.HoveredDevice);

    public void Bind(IWidgetSurface surface) => _surface = surface;

    /// <summary>A row the kit's list drew: its device (the item's id is its endpoint id).</summary>
    private void LinkRow(Themia.Sdk.Kit.KitItem item, FrameworkElement row) => Devices.Row(row, LinkKeys.DeviceOfEndpoint(item.Id));

    /// <summary>The kit's list draws its rows again: the old ones are forgotten.</summary>
    private void ForgetRows() => Devices.Clear();
}
