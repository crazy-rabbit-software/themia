using Themia.App.Widgets;
using Themia.Sdk.Devices;

namespace Themia.Widget.DeviceBatteries;

/// <summary>
/// [S-11850] [S-2056] The device batteries' sample: a fixed set of peripherals, the Bluetooth widget's (a
/// headset and a controller each seen on two paths, folded; one low, the pen; a charging controller),
/// held in place of what this PC's Windows reports.
/// </summary>
internal sealed partial class DeviceBatteriesView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        var headset = Guid.Parse("6b1c2d4e-0000-4000-8000-000000000001");
        var controller = Guid.Parse("6b1c2d4e-0000-4000-8000-000000000002");
        _source.HoldForPreview(new DeviceBatterySnapshot(DeviceBatteryStatus.Ok,
        [
            new("Bluetooth#Bluetoothe0:d4:e8:00:00:01-a4:c1:38:0b:22:9f", "Surface Headphones 2", DeviceKind.Audio, 80, DeviceBatterySource.BluetoothProperty, headset),
            new("container:headset", "Surface Headphones 2", DeviceKind.Audio, 80, DeviceBatterySource.DeviceContainer, headset, false),
            new("container:mouse", "MX Master 3S", DeviceKind.Mouse, 64, DeviceBatterySource.DeviceContainer, Guid.Parse("6b1c2d4e-0000-4000-8000-000000000003"), false),
            new("bt:keyboard", "Keychron K3", DeviceKind.Keyboard, 46, DeviceBatterySource.BluetoothProperty, Guid.Parse("6b1c2d4e-0000-4000-8000-000000000004")),
            new("bt:pad", "Xbox Wireless Controller", DeviceKind.Gamepad, 64, DeviceBatterySource.BluetoothProperty, controller),
            new("controller:pad", "Xbox Wireless Controller", DeviceKind.Gamepad, 63, DeviceBatterySource.GameController, Charging: true),
            new("le:pen", "Surface Slim Pen 2", DeviceKind.Pen, 18, DeviceBatterySource.GattBatteryService, Guid.Parse("6b1c2d4e-0000-4000-8000-000000000005")),
            new("le:watch", "Galaxy Watch6", DeviceKind.Watch, 91, DeviceBatterySource.GattBatteryService),
        ]));
        return Task.CompletedTask;
    }
}
