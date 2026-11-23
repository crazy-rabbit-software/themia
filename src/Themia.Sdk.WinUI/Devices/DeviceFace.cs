using Microsoft.UI.Xaml.Controls;
using Themia.Core.Widgets;
using Themia.Sdk.Devices;

namespace Themia.Sdk.WinUI.Devices;

/// <summary>
/// [S-16637] One device's widget face (a Drive, a USB stick, a network drive, a Bluetooth device): the device item itself
/// ([S-16631]; owner, 2026-11-23: "from left to right: icon, then title and subtitle, then right aligned percentage, under
/// all of them the progress bar"), one item of the device list's one size from the card's top edge, its card one item tall
/// (<see cref="DeviceLayout.Face"/>), so a device looks the same alone as in its list. A part the device has none of, or the
/// user left out, takes no room and is not drawn ([S-16670]). The card lights it under the pointer (its view's
/// <c>OpensLight</c>): the list's own light is off.
/// </summary>
public sealed partial class DeviceFace : Grid
{
    private readonly DeviceList _list;

    public DeviceFace(IWidgetHost host)
    {
        _list = new DeviceList(host) { LightsItems = false };
        Children.Add(_list);
    }

    /// <summary>[S-16637] Show <paramref name="item"/> without the parts <paramref name="hidden"/> leaves out.</summary>
    public void Show(DeviceItem item, IReadOnlyCollection<DevicePart> hidden, bool stock)
    {
        ArgumentNullException.ThrowIfNull(item);
        _list.Show([item], hidden, stock);
    }

    /// <summary>The card's box: the item stands from its top edge.</summary>
    public void Size(WidgetArea area) => _list.Size(area);
}
