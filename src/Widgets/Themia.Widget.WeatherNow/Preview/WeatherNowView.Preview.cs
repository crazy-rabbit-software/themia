#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2052]): <c>sample</c> is the sample (<see cref="ShowSampleAsync"/>);
/// <c>stale</c> that reading through a failed refresh; <c>failed</c> is no reading and the service out of
/// reach: the Status row ([S-6734]).
/// </summary>
internal sealed partial class WeatherNowView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "stale", "failed"];

    public Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "sample":
                return ShowSampleAsync();

            case "stale":
                // [S-7042] The answer on screen, and the last refresh failed: the header's problem glyph.
                _feed.Preview(SamplePlaceReading().Stale(FetchStatus.Offline, null), FetchStatus.Ok);
                break;

            case "failed":
                SamplePlaceReading();
                _feed.Preview(null, FetchStatus.Unreachable);
                break;
        }

        return Task.CompletedTask;
    }
}
#endif
