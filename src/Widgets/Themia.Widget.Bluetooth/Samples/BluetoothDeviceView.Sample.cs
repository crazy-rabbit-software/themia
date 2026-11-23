using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>[S-11850] [S-16662] The Bluetooth device's sample: the sample day's headphones, never this PC's radio.</summary>
internal sealed partial class BluetoothDeviceView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        HoldSample(BluetoothView.SampleSnapshot());
        return Task.CompletedTask;
    }
}
