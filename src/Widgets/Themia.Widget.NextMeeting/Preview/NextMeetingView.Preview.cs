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
/// calendar with nothing coming; <c>nolink</c> the sample with no join link anywhere, so no Join
/// ([S-2592]).
/// </summary>
internal sealed partial class NextMeetingView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "running", "none", "nolink"];

    public async Task ShowPreviewStateAsync(string state)
    {
        if (!PreviewStates.Contains(state, StringComparer.Ordinal))
        {
            return;
        }

        var now = state == "running" ? SampleDay.TodayAt(new TimeOnly(11, 20)) : SampleDay.Now;
        _clock = () => now;
        _feed.Clock = _clock;
        _feed.Use(new SampleCalendarData(empty: state == "none"));
        await _feed.RefreshAsync(force: true).ConfigureAwait(true);

        if (state == "nolink")
        {
            _feed.Events = [.. _feed.Events.Select(e => e with { JoinUrl = null })];
            Render();
        }
    }
}
#endif
