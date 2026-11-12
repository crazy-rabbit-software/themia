#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.Widget.UpNext;

/// <summary>Debug renders only ([S-1917], [S-2054]): <c>sample</c> is the sample (<see cref="ShowSampleAsync"/>); <c>none</c> a calendar with nothing coming.</summary>
internal sealed partial class UpNextView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "none"];

    public Task ShowPreviewStateAsync(string state) => state switch
    {
        "sample" => ShowSampleAsync(),
        "none" => ShowSampleCalendarAsync(empty: true),
        _ => Task.CompletedTask,
    };
}
#endif
