using Themia.App.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.UpNext;

/// <summary>
/// [S-11850] [S-1917] [S-2054] The sample day's calendar at its moment: what comes after the design review
/// at 11:00 (two more meetings today, three tomorrow), without an account.
/// </summary>
internal sealed partial class UpNextView : IWidgetSample
{
    public Task ShowSampleAsync() => ShowSampleCalendarAsync(empty: false);

    /// <summary>The sample calendar (<paramref name="empty"/>: nothing coming) read at the sample moment.</summary>
    private async Task ShowSampleCalendarAsync(bool empty)
    {
        var now = SampleDay.Now;
        _clock = () => now;
        _feed.Clock = _clock;
        _feed.Use(new SampleCalendarData(empty: empty));
        await _feed.RefreshAsync(force: true).ConfigureAwait(true);
    }
}
