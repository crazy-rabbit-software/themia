#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Accounts;
using Themia.Core.Model;
using Themia.Core.Office;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2711], [S-3606]): every calendar kind over a fixed set of sample
/// events around today — two calendars (so colours show), all-day events of one day and of several,
/// a trip across a week's end, an overnight event, overlapping meetings, birthdays, and events across
/// the year — through a fixture <see cref="ICalendarData"/>, so <c>--widget-png MonthCalendar
/// --state sample</c> draws what a signed-in calendar would without an account.
/// </summary>
internal sealed partial class CalendarView : IWidgetPreviewState
{
    /// <summary>
    /// <c>sample</c>: the fixture; <c>selected</c>: the day after tomorrow selected on the view
    /// ([S-3620]); <c>linked</c>: another widget's pointer on the design review ([S-3623]);
    /// <c>days</c>: the kind's own range ([S-2719], [S-3636]): the week the next four days from today,
    /// the agenda today and tomorrow.
    /// </summary>
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "selected", "linked", "days"];

    public async Task ShowPreviewStateAsync(string state)
    {
        if (!PreviewStates.Contains(state, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _agendaPage = 0;

        if (string.Equals(state, "days", StringComparison.OrdinalIgnoreCase))
        {
            _config = _layout switch
            {
                CalendarLayout.Week => new WeekCalendarConfig { Days = 4, FirstDay = WeekFirstDay.Today },
                CalendarLayout.Agenda => new AgendaConfig { AgendaDays = 2 },
                _ => _config,
            };
        }

        UseData(new SampleCalendarData());
        await RefreshAsync(force: true).ConfigureAwait(true);

        if (string.Equals(state, "selected", StringComparison.OrdinalIgnoreCase))
        {
            OnSelectedElsewhere(new Themia.Core.Links.DateLink(Today.AddDays(2)));
        }
        else if (string.Equals(state, "linked", StringComparison.OrdinalIgnoreCase))
        {
            _linkedHover = new Themia.Core.Links.EventLink("sample:work", null, "Design review-0");
            Render();
        }
    }


    /// <summary>The fixture: the same sample week whatever the range, filtered to it.</summary>
    private sealed class SampleCalendarData : ICalendarData
    {
        public event Action? Changed
        {
            add { }
            remove { }
        }

        public Task<CalendarSlice> LoadAsync(CalendarConfig config, DateOnly first, DateOnly last, bool force, CancellationToken cancellation)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var targets = new List<CalendarTarget> { new("sample:work", null), new("sample:home", "home") };
            var events = new List<CalendarEvent>();

            CalendarEvent At(int calendar, string subject, int dayOffset, int hour, int minute, int minutes, string? location = null)
            {
                var day = today.AddDays(dayOffset).ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Local);
                var start = new DateTimeOffset(day);
                return new CalendarEvent
                {
                    Id = $"{subject}-{dayOffset}",
                    Subject = subject,
                    Start = start,
                    End = start.AddMinutes(minutes),
                    Location = location,
                    AccountId = targets[calendar].AccountId,
                    CalendarId = targets[calendar].CalendarId,
                    SourceIndex = calendar,
                };
            }

            CalendarEvent AllDay(int calendar, string subject, int fromOffset, int days)
                => CalendarEvent.AllDayEvent($"{subject}-{fromOffset}", subject, today.AddDays(fromOffset), today.AddDays(fromOffset + days))
                    with { AccountId = targets[calendar].AccountId, CalendarId = targets[calendar].CalendarId, SourceIndex = calendar };

            events.Add(At(0, "Standup", 0, 9, 0, 15, "Teams"));
            events.Add(At(0, "Design review", 0, 10, 30, 60, "Room 4.12"));
            events.Add(At(1, "Dentist", 0, 11, 0, 45, "Bahnhofstrasse 3"));
            events.Add(At(0, "1:1 with Sam", 0, 14, 0, 30));
            events.Add(At(1, "Pick up the kids", 0, 16, 30, 30));
            events.Add(AllDay(1, "Public holiday", 0, 1));
            events.Add(At(0, "Standup", 1, 9, 0, 15, "Teams"));
            events.Add(At(0, "Quarterly planning with product, design and engineering leads", 1, 13, 0, 120, "Board room"));
            events.Add(At(1, "Yoga", 1, 18, 0, 60));
            events.Add(AllDay(0, "Offsite in Lisbon", 2, 4));
            events.Add(At(1, "Night train", 2, 22, 0, 480));
            events.Add(At(0, "Release", 3, 15, 0, 60));
            events.Add(At(0, "Retro", -1, 16, 0, 60));
            events.Add(At(1, "Cinema", -2, 20, 0, 150));
            events.Add(AllDay(1, "Conference", -9, 2));
            events.Add(At(0, "Budget review", 8, 10, 0, 90));
            events.Add(At(1, "Dinner with Ana", 12, 19, 30, 120));

            // A year's worth of rhythm: a meeting most weeks, a few longer trips.
            for (var week = -40; week <= 40; week++)
            {
                if (week % 3 != 0 && Math.Abs(week) > 2)
                {
                    events.Add(At(week % 2 == 0 ? 0 : 1, "Monthly sync", week * 7 + 1, 11, 0, 60));
                }
            }

            events.Add(AllDay(1, "Summer holiday", -100, 10));
            events.Add(AllDay(1, "Ski week", 90, 6));

            var (from, to) = CalendarRules.FetchWindow(first, last, TimeZoneInfo.Local);
            var inRange = events.Where(e => e.AllDay
                ? e.FirstDay <= last && e.EndDay > first
                : e.Start < to && e.End > from).ToList();

            var birthdays = new List<Birthday>
            {
                new("Mia Chen", today.Month, today.Day, 1994),
                new("Jonas Weber", today.AddDays(3).Month, today.AddDays(3).Day, null),
                new("Lena Fischer", today.AddDays(40).Month, today.AddDays(40).Day, 1988),
            };

            var providers = new Dictionary<string, AccountProvider>(StringComparer.Ordinal) { ["sample:work"] = AccountProvider.Microsoft, ["sample:home"] = AccountProvider.Google };
            var labels = new Dictionary<string, string>(StringComparer.Ordinal) { ["sample:work"] = "Work", ["sample:home"] = "Home" };
            return Task.FromResult(new CalendarSlice(CalendarSliceState.Asked, inRange, birthdays, targets, providers, labels, [], true));
        }
    }
}
#endif
