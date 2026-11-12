#if DEBUG
using Themia.App.Services.Brightness;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>Debug renders only ([S-1917]): made-up displays, never this PC's; <c>levels</c> is the sample (<see cref="ShowSampleAsync"/>).</summary>
internal sealed partial class BrightnessView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["levels", "dim", "unavailable", "pending"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "levels")
        {
            return ShowSampleAsync();
        }

        _preview = state switch
        {
            "dim" => new BrightnessSnapshot(true, [new(@"\\.\SAMPLE1", BrightnessPath.Ddc, 12)]),
            "unavailable" => new BrightnessSnapshot(true, []),
            "pending" => BrightnessSnapshot.Pending,
            _ => _preview,
        };

        Lay();
        return Task.CompletedTask;
    }
}
#endif
