using Themia.App.Services.Web;
using Themia.Core.Widgets;

namespace Themia.Widget.WeatherFacts;

/// <summary>
/// Debug renders only ([S-1917], [S-2052], [S-14605]): <c>sample</c> is the sample (<see cref="ShowSampleAsync"/>);
/// <c>stale</c> is that reading through a failed refresh; <c>failed</c> no reading and the service out of reach ([S-6734]);
/// the base's <c>arrow</c> and <c>line</c> draw the sample with each trend, over the sample day's hours.
/// </summary>
internal sealed partial class WeatherFactView
{
    protected override IReadOnlyList<string> OwnPreviewStates { get; } = ["sample", "stale", "failed"];

    protected override Task ShowOwnPreviewStateAsync(string state)
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
