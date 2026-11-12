#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Accounts;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Tasks;
using Themia.Sdk;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2711], [S-2054], [S-2055]): <c>sample</c> is the sample day's
/// week at its moment (two calendars, the trips, today's five events) with the errands due by today,
/// one overdue; <c>free</c> is the same week with nothing today and no errands.
/// </summary>
internal sealed partial class WeekPlannerView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "free"];

    public async Task ShowPreviewStateAsync(string state)
    {
        if (!PreviewStates.Contains(state, StringComparer.Ordinal))
        {
            return;
        }

        var free = state == "free";
        var now = SampleDay.Now;
        _clock = () => now;

        var today = SampleDay.Today;
        _sampleTasks = free ? [] :
        [
            .. SampleTasks.DueByToday.Select((task, i) =>
                new TaskItem($"t{i + 1}", "l", task.Title, false, DueRaw: TaskDue.Wire(today.AddDays(task.DueInDays ?? 0)))),
        ];

        ReleaseTasks();
        UseData(new SampleCalendarData(freeToday: free));
        await RefreshAsync(force: true).ConfigureAwait(true);
    }
}
#endif
