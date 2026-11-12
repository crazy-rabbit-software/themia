#if DEBUG
using Themia.App.Services;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-2056]): <c>devices</c> is the sample (<see cref="ShowSampleAsync"/>), so the
/// battery column ([S-3252]..[S-3254]) can be looked at on a machine whose devices report none; <c>off</c>
/// is the radio switched off (the Status row, [S-244]), never this PC's radio.
/// </summary>
internal sealed partial class BluetoothView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["devices", "off"];

    public Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "devices":
                return ShowSampleAsync();
            case "off":
                HoldSample(BluetoothSnapshot.Of(BluetoothStatus.RadioOff));
                break;
        }

        return Task.CompletedTask;
    }
}
#endif
