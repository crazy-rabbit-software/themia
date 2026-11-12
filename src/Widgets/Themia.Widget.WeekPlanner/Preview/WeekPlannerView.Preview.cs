#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2711], [S-2054], [S-2055]): <c>sample</c> is the sample
/// (<see cref="ShowSampleAsync"/>); <c>free</c> is the same week with nothing today and no errands.
/// </summary>
internal sealed partial class WeekPlannerView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "free"];

    public Task ShowPreviewStateAsync(string state) => state switch
    {
        "sample" => ShowSampleAsync(),
        "free" => ShowSampleWeekAsync([], freeToday: true),
        _ => Task.CompletedTask,
    };
}
#endif
