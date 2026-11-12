#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.Widget.WeatherFacts;

/// <summary>
/// Debug renders only ([S-1917], [S-2052]): <c>sample</c> is the sample (<see cref="ShowSampleAsync"/>);
/// <c>stale</c> is that reading through a failed refresh; <c>failed</c> no reading and the service out of reach ([S-6734]).
/// </summary>
internal sealed partial class WeatherFactsView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "stale", "failed"];

    public Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "sample":
                return ShowSampleAsync();

            case "stale":
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
