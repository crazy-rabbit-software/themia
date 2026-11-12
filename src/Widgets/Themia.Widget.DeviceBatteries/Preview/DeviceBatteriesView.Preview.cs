#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Sdk.Devices;

namespace Themia.Widget.DeviceBatteries;

/// <summary>
/// Debug renders only ([S-1917]): <c>--widget-png DeviceBatteries --state sample</c> draws the sample
/// (<see cref="ShowSampleAsync"/>); <c>empty</c> no device reporting, <c>failed</c> a failed read,
/// <c>loading</c> the first read out. Without a state the render shows what this PC's Windows reports.
/// </summary>
internal sealed partial class DeviceBatteriesView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "empty", "failed", "loading"];

    public Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "sample":
                return ShowSampleAsync();
            case "empty":
                _source.HoldForPreview(new DeviceBatterySnapshot(DeviceBatteryStatus.Ok, []));
                break;
            case "failed":
                _source.HoldForPreview(new DeviceBatterySnapshot(DeviceBatteryStatus.Failed, []));
                break;
            case "loading":
                _source.HoldForPreview(DeviceBatterySnapshot.Loading);
                break;
        }

        return Task.CompletedTask;
    }
}
#endif
