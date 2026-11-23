using Themia.App.Widgets;
using Themia.Sdk;
using Themia.Sdk.Samples;

namespace Themia.Widget.Meetings;

/// <summary>
/// [S-11850] [S-2711] [S-2054] The Meeting's sample: the sample day's calendar at its moment, the design review at 11:00,
/// eighteen minutes away, with Join, without an account.
/// </summary>
internal sealed partial class MeetingView : IWidgetSample
{
    public Task ShowSampleAsync() => ShowCalendarAsync(SampleDay.Now, empty: false);

    /// <summary>Read <see cref="SampleCalendarData"/> (or an empty calendar) at <paramref name="now"/>.</summary>
    internal async Task ShowCalendarAsync(DateTimeOffset now, bool empty)
    {
        _clock = () => now;
        _feed.Clock = _clock;
        _feed.Use(new SampleCalendarData(empty: empty));
        await _feed.RefreshAsync(force: true).ConfigureAwait(true);
    }
}

/// <summary>
/// [S-11850] [S-1917] [S-2054] The Meetings' sample: the sample day's calendar at its moment, the design review at 11:00 and
/// what comes after it (two more meetings today, three tomorrow), without an account.
/// </summary>
internal sealed partial class MeetingsView : IWidgetSample
{
    public Task ShowSampleAsync() => ShowCalendarAsync(SampleDay.Now, empty: false);

    /// <summary>The sample calendar (<paramref name="empty"/>: nothing coming) read at <paramref name="now"/>.</summary>
    internal async Task ShowCalendarAsync(DateTimeOffset now, bool empty)
    {
        _clock = () => now;
        _feed.Clock = _clock;
        _binding.Clock = _clock;
        _feed.Use(new SampleCalendarData(empty: empty));
        await _feed.RefreshAsync(force: true).ConfigureAwait(true);
    }
}
