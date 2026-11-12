#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Accounts;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Sdk;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2711], [S-3606], [S-2054]): every calendar style over the sample
/// day's schedule at its moment ([S-2050]) — two calendars (so colours show), trips of several days
/// and a weekend away, an overnight train, overlapping meetings, birthdays, and an all-hands across
/// the year — through <see cref="SampleCalendarData"/>, so <c>--widget-png Calendar --style month
/// --state sample</c> draws what a signed-in calendar would without an account.
/// </summary>
internal sealed partial class CalendarView : IWidgetPreviewState
{
    /// <summary>
    /// <c>sample</c>: the fixture; <c>selected</c>: the day after tomorrow selected on the view
    /// ([S-3620]); <c>linked</c>: another widget's pointer on the design review (the next meeting) ([S-3623]);
    /// <c>days</c>: the style's own range ([S-2719], [S-3636]): the week the next four days from today,
    /// the agenda today and tomorrow; <c>stale</c>: the sample with the Work calendar's last sync failed
    /// (no network), its events held from 20 minutes ago; <c>empty</c>: no event at all.
    /// </summary>
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "selected", "linked", "days", "stale", "empty"];

    public async Task ShowPreviewStateAsync(string state)
    {
        if (!PreviewStates.Contains(state, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _agendaPage = 0;
        var now = SampleDay.Now;
        _clock = () => now;

        if (string.Equals(state, "days", StringComparison.OrdinalIgnoreCase))
        {
            var days = (CalendarWidgetConfig)_config.Clone();
            days.WeekDays = 4;
            days.WeekFrom = WeekFirstDay.Today;
            days.AgendaDays = 2;
            _config = days;
        }

        UseData(new SampleCalendarData(
            empty: string.Equals(state, "empty", StringComparison.OrdinalIgnoreCase),
            stale: string.Equals(state, "stale", StringComparison.OrdinalIgnoreCase)));
        await RefreshAsync(force: true).ConfigureAwait(true);

        if (string.Equals(state, "selected", StringComparison.OrdinalIgnoreCase))
        {
            OnSelectedElsewhere(new Themia.Core.Links.DateLink(Today.AddDays(2)));
        }
        else if (string.Equals(state, "linked", StringComparison.OrdinalIgnoreCase))
        {
            _linkedHover = new Themia.Core.Links.EventLink(SampleSchedule.WorkAccount, null, SampleSchedule.NextMeetingId);
            Render();
        }
    }
}
#endif
