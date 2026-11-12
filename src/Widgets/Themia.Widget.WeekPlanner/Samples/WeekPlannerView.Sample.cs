using Themia.Core.Office;
using Themia.Core.Tasks;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-1917] [S-2711] [S-2054] [S-2055] The week's sample: the sample day's week at its moment (two
/// calendars, the trips, today's five events) with the errands due by today, one overdue.
/// </summary>
internal sealed partial class WeekPlannerView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        var today = SampleDay.Today;
        return ShowSampleWeekAsync(
        [
            .. SampleTasks.DueByToday.Select((task, i) =>
                new TaskItem($"t{i + 1}", "l", task.Title, false, DueRaw: TaskDue.Wire(today.AddDays(task.DueInDays ?? 0)))),
        ], freeToday: false);
    }

    /// <summary>The sample week at the sample moment with <paramref name="tasks"/> (<paramref name="freeToday"/>: nothing today).</summary>
    private async Task ShowSampleWeekAsync(IReadOnlyList<TaskItem> tasks, bool freeToday)
    {
        var now = SampleDay.Now;
        _clock = () => now;
        _sampleTasks = tasks;
        ReleaseTasks();
        UseData(new SampleCalendarData(freeToday: freeToday));
        await RefreshAsync(force: true).ConfigureAwait(true);
    }
}
