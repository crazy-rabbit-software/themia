using Themia.App.Services;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-2056] The Bluetooth widget's sample: the sample day's paired devices (the peripheral
/// batteries' too, one low) with and without a battery reading, never this PC's radio.
/// </summary>
internal sealed partial class BluetoothView : IWidgetSample
{
    /// <summary>[S-11850] The sample day's paired devices.</summary>
    internal static BluetoothSnapshot SampleSnapshot() => new(BluetoothStatus.Ok,
    [
        new BluetoothDeviceEntry("Surface Headphones 2", "Bluetooth#Bluetoothe0:d4:e8:00:00:01-a4:c1:38:0b:22:9f", true, BluetoothDeviceKind.Audio, 80),
        new BluetoothDeviceEntry("MX Master 3S", "fixture:mouse", true, BluetoothDeviceKind.Mouse, 64),
        new BluetoothDeviceEntry("Keychron K3", "fixture:keyboard", true, BluetoothDeviceKind.Keyboard, 46),
        new BluetoothDeviceEntry("Surface Slim Pen 2", "fixture:pen", true, BluetoothDeviceKind.Pen, 18),
        new BluetoothDeviceEntry("Pixel 8", "fixture:phone", false, BluetoothDeviceKind.Phone, null),
    ]);

    public Task ShowSampleAsync()
    {
        HoldSample(SampleSnapshot());
        return Task.CompletedTask;
    }
}
