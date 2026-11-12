using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-2054] The calendar's sample: the sample day's schedule at its moment ([S-2050]) — two
/// calendars (so colours show), trips of several days and a weekend away, an overnight train,
/// overlapping meetings, birthdays, and an all-hands across the year — through
/// <see cref="SampleCalendarData"/>, so every style draws what a signed-in calendar would without an account.
/// </summary>
internal sealed partial class CalendarView : IWidgetSample
{
    public Task ShowSampleAsync() => HoldSampleAsync(empty: false, stale: false);

    /// <summary>The sample schedule at the sample moment (<paramref name="empty"/>: no event at all; <paramref name="stale"/>: the Work calendar's last sync failed).</summary>
    internal async Task HoldSampleAsync(bool empty, bool stale)
    {
        _agendaPage = 0;
        var now = SampleDay.Now;
        _clock = () => now;
        UseData(new SampleCalendarData(empty: empty, stale: stale));
        await RefreshAsync(force: true).ConfigureAwait(true);
    }
}
