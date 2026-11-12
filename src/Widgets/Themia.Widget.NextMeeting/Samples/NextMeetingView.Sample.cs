using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-2711] [S-2054] The next meeting's sample: the sample day's calendar at its moment, the design
/// review at 11:00, eighteen minutes away, without an account.
/// </summary>
internal sealed partial class NextMeetingView : IWidgetSample
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
