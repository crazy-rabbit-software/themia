using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.Widget.Battery;

/// <summary>
/// Debug renders only (Preview/, [S-11850]): a made-up battery, never this PC's: the sample (on battery), charging (its time
/// to full from the pack), low (its bar Critical) and none (a desktop, [S-1940]).
/// </summary>
internal sealed partial class BatteryView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "charging", "low", "unavailable"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            return ShowSampleAsync();
        }

        _pack.Release();
        _power.Release();
        _held = state switch
        {
            "charging" => (new BatteryPower(true, 64, true, true, null), new BatteryPack(false, 57_000, 52_000, 33_280, 18_000, 12_400, 210, 31, "Li-ion", "Sample", 1)),
            "low" => (new BatteryPower(true, 12, false, false, TimeSpan.FromMinutes(25)), null),
            "unavailable" => (new BatteryPower(false, null, true, false, null), null),
            _ => _held,
        };
        Show();
        return Task.CompletedTask;
    }
}
