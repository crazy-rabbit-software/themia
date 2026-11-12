#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Accounts;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Sdk;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2711], [S-2054]): the sample day's calendar at its moment, so
/// <c>--widget-png NextMeeting --state sample</c> draws the design review at 11:00, eighteen minutes
/// away, without an account; <c>running</c> is the same meeting under way (11:20); <c>none</c> a
/// calendar with nothing coming. The schedule's later meetings (today's, then the next days') are what
/// a tall card lists under the meeting ([S-2160]).
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

        var now = state == "running" ? SampleDay.TodayAt(new TimeOnly(11, 20)) : SampleDay.Now;
        _clock = () => now;
        UseData(new SampleCalendarData(empty: state == "none"));
        await RefreshAsync(force: true).ConfigureAwait(true);
    }
}
#endif
