#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2052]): <c>sample</c> is the sample (<see cref="ShowSampleAsync"/>,
/// in the card's style: <c>--style rows|columns|curve|grid</c>); <c>stale</c> the same week through a failed refresh.
/// </summary>
internal sealed partial class WeatherForecastView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "stale"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            return ShowSampleAsync();
        }

        if (state != "stale")
        {
            return Task.CompletedTask;
        }

        HoldSampleWeek();

        // [S-7042] The answer on screen, and the last refresh failed: the header's problem glyph.
        _forecast = _forecast!.Stale(FetchStatus.Offline, null);
        PublishPlace();
        Render();
        return Task.CompletedTask;
    }
}
#endif
