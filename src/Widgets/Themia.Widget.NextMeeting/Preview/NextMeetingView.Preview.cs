#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2711], [S-2054]): <c>sample</c> is the next meeting's sample
/// (<see cref="ShowSampleAsync"/>); <c>running</c> is the same meeting under way (11:20); <c>none</c> a
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

        if (state == "sample")
        {
            await ShowSampleAsync().ConfigureAwait(true);
            return;
        }

        var now = state == "running" ? SampleDay.TodayAt(new TimeOnly(11, 20)) : SampleDay.Now;
        await ShowCalendarAsync(now, empty: state == "none").ConfigureAwait(true);

        if (state == "nolink")
        {
            _feed.Events = [.. _feed.Events.Select(e => e with { JoinUrl = null })];
            Render();
        }
    }
}
#endif
