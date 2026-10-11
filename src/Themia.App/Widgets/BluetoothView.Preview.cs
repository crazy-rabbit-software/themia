#if DEBUG
using Themia.App.Services;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only: a fixed set of paired devices with and without a battery reading, so the
/// battery column ([S-3252]..[S-3254]) can be looked at on a machine whose devices report none.
/// </summary>
internal sealed partial class BluetoothView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["devices"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state != "devices")
        {
            return Task.CompletedTask;
        }

        // The radio pass still out would draw over the fixture when it lands: it is now stale.
        _inFlight?.Cancel();
        _inFlight = null;
        _generation++;
        SetBusy(false);

        _snapshot = new BluetoothSnapshot(BluetoothStatus.Ok,
        [
            new BluetoothDeviceEntry("Surface Headphones", "fixture:headphones", true, BluetoothDeviceKind.Audio, 80),
            new BluetoothDeviceEntry("MX Master 3S Mouse", "fixture:mouse", true, BluetoothDeviceKind.Mouse, 15),
            new BluetoothDeviceEntry("Keychron K2 Keyboard", "fixture:keyboard", true, BluetoothDeviceKind.Keyboard, null),
            new BluetoothDeviceEntry("Pixel 8", "fixture:phone", false, BluetoothDeviceKind.Phone, null),
        ]);

        Render();
        return Task.CompletedTask;
    }
}
#endif
