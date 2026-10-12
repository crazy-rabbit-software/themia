#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Accounts;
using Themia.Core.Model;
using Themia.Core.Office;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2711]): a fixture calendar, so <c>--widget-png NextMeeting
/// --state sample</c> draws a Teams meeting twelve minutes away without an account; <c>running</c>
/// is the same meeting under way; <c>none</c> a calendar with nothing coming.
/// </summary>
internal sealed partial class NextMeetingView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "running", "none"];

    public async Task ShowPreviewStateAsync(string state)
    {
        if (!PreviewStates.Contains(state, StringComparer.Ordinal))
        {
            return;
        }

        var clock = DateTime.Today.AddHours(10).AddMinutes(state == "running" ? 40 : 18);
        var now = new DateTimeOffset(clock, TimeZoneInfo.Local.GetUtcOffset(clock));
        _clock = () => now;
        UseData(new SampleData(state == "none"));
        await RefreshAsync(force: true).ConfigureAwait(true);
    }

    private sealed class SampleData(bool empty) : ICalendarData
    {
        public event Action? Changed
        {
            add { }
            remove { }
        }

        public Task<CalendarSlice> LoadAsync(CalendarConfig config, DateOnly first, DateOnly last, bool force, CancellationToken cancellation)
        {
            var today = DateTime.Today;
            CalendarEvent At(string subject, int hour, int minute, int minutes) => new()
            {
                Id = subject,
                Subject = subject,
                Start = new DateTimeOffset(today.AddHours(hour).AddMinutes(minute)),
                End = new DateTimeOffset(today.AddHours(hour).AddMinutes(minute + minutes)),
                AccountId = "sample:work",
            };

            List<CalendarEvent> events = empty ? [] :
            [
                At("Standup", 9, 0, 15),
                At("Design review: widget ladder and the new picker", 10, 30, 60) with
                {
                    Location = "Room 4.12 · Microsoft Teams",
                    Organizer = "Ana Silva",
                    JoinUrl = "https://teams.microsoft.com/l/meetup-join/19%3ameeting_sample%40thread.v2/0",
                },
                At("Lunch", 12, 0, 60) with { Declined = true },
                At("1:1 with Sam", 14, 0, 30),
                CalendarEvent.AllDayEvent("holiday", "Public holiday", DateOnly.FromDateTime(today), DateOnly.FromDateTime(today).AddDays(1)),
            ];

            var providers = new Dictionary<string, AccountProvider>(StringComparer.Ordinal) { ["sample:work"] = AccountProvider.Microsoft };
            var labels = new Dictionary<string, string>(StringComparer.Ordinal) { ["sample:work"] = "Work" };
            return Task.FromResult(new CalendarSlice(CalendarSliceState.Asked, events, [], [new CalendarTarget("sample:work", null)], providers, labels, [], true));
        }
    }
}
#endif
