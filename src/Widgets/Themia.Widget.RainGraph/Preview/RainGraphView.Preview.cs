using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.RainGraph;

/// <summary>
/// Debug renders only ([S-11852]): <c>sample</c> the sample day; <c>storm</c> a summer evening's thunderstorm (the
/// heavy hour in Caution); <c>stale</c> the sample kept through a failed refresh; <c>loading</c>, <c>empty</c> and
/// <c>failed</c> the Status row.
/// </summary>
internal sealed partial class RainGraphView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "storm", "stale", "loading", "empty", "failed"];

    public Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "sample":
                return ShowSampleAsync();
            case "storm":
                Hold(SampleHours.Summer(), FetchStatus.Ok);
                break;
            case "stale":
                Hold(SampleHours.Weather(), FetchStatus.Ok, stale: true);
                break;
            case "loading":
                Hold(null, FetchStatus.Loading);
                break;
            case "empty":
                Hold(new WeatherHours([new WeatherHour(SampleDay.HoursAgo(30)) { Precipitation = 0 }], []), FetchStatus.Ok);
                break;
            case "failed":
                Hold(null, FetchStatus.Unreachable);
                break;
        }

        return Task.CompletedTask;
    }
}
