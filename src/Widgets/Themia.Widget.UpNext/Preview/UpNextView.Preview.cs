#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.App.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.UpNext;

/// <summary>
/// Debug renders only ([S-1917], [S-2054]): the sample day's calendar at its moment, so
/// <c>--widget-png UpNext --state sample</c> lists what comes after the design review at 11:00 (two more
/// meetings today, three tomorrow) without an account; <c>none</c> a calendar with nothing coming.
/// </summary>
internal sealed partial class UpNextView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "none"];

    public async Task ShowPreviewStateAsync(string state)
    {
        if (!PreviewStates.Contains(state, StringComparer.Ordinal))
        {
            return;
        }

        var now = SampleDay.Now;
        _clock = () => now;
        _feed.Clock = _clock;
        _feed.Use(new SampleCalendarData(empty: state == "none"));
        await _feed.RefreshAsync(force: true).ConfigureAwait(true);
    }
}
#endif
