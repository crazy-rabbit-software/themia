#if DEBUG
using Themia.App.Services.Brightness;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>Debug renders only ([S-1917]): made-up displays, never this PC's.</summary>
internal sealed partial class BrightnessView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["levels", "dim", "none", "pending"];

    public Task ShowPreviewStateAsync(string state)
    {
        _preview = state switch
        {
            "levels" => new BrightnessSnapshot(true, [new(@"\\.\SAMPLE1", BrightnessPath.BuiltIn, 70), new(@"\\.\SAMPLE2", BrightnessPath.Ddc, 45)]),
            "dim" => new BrightnessSnapshot(true, [new(@"\\.\SAMPLE1", BrightnessPath.Ddc, 12)]),
            "none" => new BrightnessSnapshot(true, []),
            "pending" => BrightnessSnapshot.Pending,
            _ => _preview,
        };

        Lay();
        return Task.CompletedTask;
    }
}
#endif
