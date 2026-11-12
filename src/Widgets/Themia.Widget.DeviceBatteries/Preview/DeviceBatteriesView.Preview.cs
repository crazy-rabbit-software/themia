#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Sdk.Devices;

namespace Themia.Widget.DeviceBatteries;

/// <summary>
/// Debug renders only ([S-1917]): <c>--widget-png DeviceBatteries --state sample</c> draws a fixed set
/// of peripherals, the Bluetooth widget's ([S-2056]: a headset and a controller each seen on two paths, folded; one low, the pen; a
/// charging controller); <c>empty</c> no device reporting, <c>failed</c> a failed read, <c>loading</c>
/// the first read out. Without a state the render shows what this PC's Windows reports.
/// </summary>
internal sealed partial class DeviceBatteriesView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "empty", "failed", "loading"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (!PreviewStates.Contains(state, StringComparer.Ordinal))
        {
            return Task.CompletedTask;
        }

        var headset = Guid.Parse("6b1c2d4e-0000-4000-8000-000000000001");
        var controller = Guid.Parse("6b1c2d4e-0000-4000-8000-000000000002");

        var snapshot = state switch
        {
            "sample" => new DeviceBatterySnapshot(DeviceBatteryStatus.Ok,
            [
                new("bt:headset", "Surface Headphones 2", DeviceKind.Audio, 80, DeviceBatterySource.BluetoothProperty, headset),
                new("container:headset", "Surface Headphones 2", DeviceKind.Audio, 80, DeviceBatterySource.DeviceContainer, headset, false),
                new("container:mouse", "MX Master 3S", DeviceKind.Mouse, 64, DeviceBatterySource.DeviceContainer, Guid.Parse("6b1c2d4e-0000-4000-8000-000000000003"), false),
                new("bt:keyboard", "Keychron K3", DeviceKind.Keyboard, 46, DeviceBatterySource.BluetoothProperty, Guid.Parse("6b1c2d4e-0000-4000-8000-000000000004")),
                new("bt:pad", "Xbox Wireless Controller", DeviceKind.Gamepad, 64, DeviceBatterySource.BluetoothProperty, controller),
                new("controller:pad", "Xbox Wireless Controller", DeviceKind.Gamepad, 63, DeviceBatterySource.GameController, Charging: true),
                new("le:pen", "Surface Slim Pen 2", DeviceKind.Pen, 18, DeviceBatterySource.GattBatteryService, Guid.Parse("6b1c2d4e-0000-4000-8000-000000000005")),
                new("le:watch", "Galaxy Watch6", DeviceKind.Watch, 91, DeviceBatterySource.GattBatteryService),
            ]),
            "empty" => new DeviceBatterySnapshot(DeviceBatteryStatus.Ok, []),
            "failed" => new DeviceBatterySnapshot(DeviceBatteryStatus.Failed, []),
            _ => DeviceBatterySnapshot.Loading,
        };

        _source.HoldForPreview(snapshot);
        return Task.CompletedTask;
    }
}
#endif
