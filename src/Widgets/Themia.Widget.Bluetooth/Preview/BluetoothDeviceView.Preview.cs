using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>Debug renders only: the sample (<see cref="ShowSampleAsync"/>), a device paired no more, the radio off.</summary>
internal sealed partial class BluetoothDeviceView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "gone", "off"];

    public Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "sample":
                return ShowSampleAsync();
            case "gone":
                _config = new Themia.Core.Model.BluetoothDeviceConfig { Device = "fixture:gone" };
                HoldSample(BluetoothView.SampleSnapshot());
                break;
            case "off":
                HoldSample(BluetoothSnapshot.Of(BluetoothStatus.RadioOff));
                break;
        }

        return Task.CompletedTask;
    }
}
