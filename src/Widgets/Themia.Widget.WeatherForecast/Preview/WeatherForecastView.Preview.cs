using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2052]): <c>sample</c> is the sample (<see cref="ShowSampleAsync"/>,
/// in the card's style: <c>--style rows|columns|curve|grid</c>); <c>stale</c> the same week through a failed refresh;
/// <c>failed</c> no forecast and the service out of reach, the days kept ([S-12813]).
/// </summary>
internal sealed partial class WeatherForecastView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "stale", "failed"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            return ShowSampleAsync();
        }

        if (state == "failed")
        {
            HoldSampleWeek();
            _feed.Preview(null, FetchStatus.Unreachable);
            return Task.CompletedTask;
        }

        if (state != "stale")
        {
            return Task.CompletedTask;
        }

        HoldSampleWeek();

        // [S-7042] The answer on screen, and the last refresh failed: the status's problem glyph.
        _feed.Preview(_feed.Held!.Stale(FetchStatus.Offline, null), FetchStatus.Ok);
        return Task.CompletedTask;
    }
}
