#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Accounts;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Tasks;
using Themia.Sdk;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2711]): <c>sample</c> is a fixture week at 08:40 today (two
/// calendars, an all-day event, a busy today, quieter days after) with three tasks due today and one
/// overdue; <c>free</c> is the same week with nothing today and no tasks.
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
        var clock = DateTime.Today.AddHours(8).AddMinutes(40);
        var now = new DateTimeOffset(clock, TimeZoneInfo.Local.GetUtcOffset(clock));
        _clock = () => now;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var due = TaskDue.Wire(today);
        _sampleTasks = free ? [] :
        [
            new TaskItem("t1", "l", "Send the invoice", false, DueRaw: due),
            new TaskItem("t2", "l", "Book the dentist appointment", false, DueRaw: TaskDue.Wire(today.AddDays(-2))),
            new TaskItem("t3", "l", "Pick up the parcel", false, DueRaw: due),
            new TaskItem("t4", "l", "Water the plants", false, DueRaw: due),
        ];

        ReleaseTasks();
        UseData(new SampleData(free));
        await RefreshAsync(force: true).ConfigureAwait(true);
    }

    private sealed class SampleData(bool freeToday) : ICalendarData
    {
        public event Action? Changed
        {
            add { }
            remove { }
        }

        public Task<CalendarSlice> LoadAsync(CalendarConfig config, DateOnly first, DateOnly last, bool force, CancellationToken cancellation)
        {
            var today = DateTime.Today;
            CalendarEvent At(string subject, int day, int hour, int minute, int minutes, string account = "sample:work", int index = 0) => new()
            {
                Id = $"{subject}-{day}",
                Subject = subject,
                Start = new DateTimeOffset(today.AddDays(day).AddHours(hour).AddMinutes(minute)),
                End = new DateTimeOffset(today.AddDays(day).AddHours(hour).AddMinutes(minute + minutes)),
                AccountId = account,
                CalendarId = account == "sample:home" ? "home" : null,
                SourceIndex = index,
            };

            var firstDay = DateOnly.FromDateTime(today);
            List<CalendarEvent> events =
            [
                At("Standup", 1, 9, 0, 15),
                At("Design review", 1, 10, 30, 60),
                At("Gym", 1, 18, 0, 60, "sample:home", 1),
                At("Planning", 2, 14, 0, 45),
                CalendarEvent.AllDayEvent("trip", "Trip to Lisbon", firstDay.AddDays(3), firstDay.AddDays(5)) with { AccountId = "sample:home", CalendarId = "home", SourceIndex = 1 },
                At("Dinner with Sam", 4, 19, 30, 120, "sample:home", 1),
                At("Retro", 6, 11, 0, 60),
            ];

            if (!freeToday)
            {
                events.AddRange(
                [
                    At("Standup", 0, 9, 0, 15),
                    At("1:1 with Ana", 0, 11, 0, 30),
                    At("Lunch with the team", 0, 12, 30, 60, "sample:home", 1),
                    At("Widget ladder review", 0, 15, 0, 60),
                ]);
            }

            var providers = new Dictionary<string, ProviderId>(StringComparer.Ordinal)
            {
                ["sample:work"] = HostProviders.Microsoft,
                ["sample:home"] = HostProviders.Google,
            };
            var labels = new Dictionary<string, string>(StringComparer.Ordinal) { ["sample:work"] = "Work", ["sample:home"] = "Home" };
            List<CalendarTarget> targets = [new("sample:work", null), new("sample:home", "home")];
            return Task.FromResult(new CalendarSlice(CalendarSliceState.Asked, events, [], targets, providers, labels, [], true));
        }
    }
}
#endif
