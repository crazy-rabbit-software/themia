#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.PressureGraph;

/// <summary>
/// Debug renders only ([S-11852]): <c>sample</c> the sample day; <c>falling</c> a summer day's steady fall; <c>stale</c> the sample kept
/// through a failed refresh; <c>loading</c>, <c>empty</c> (an answer with nothing for the window) and <c>failed</c>
/// the Status row.
/// </summary>
internal sealed partial class PressureGraphView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "falling", "stale", "loading", "empty", "failed"];

    public Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "sample":
                return ShowSampleAsync();
            case "falling":
                Hold(SampleHours.Summer(), FetchStatus.Ok);
                break;
            case "stale":
                Hold(SampleHours.Weather(), FetchStatus.Ok, stale: true);
                break;
            case "loading":
                Hold(null, FetchStatus.Loading);
                break;
            case "empty":
                Hold(new WeatherHours([new WeatherHour(SampleDay.HoursAgo(30))], []), FetchStatus.Ok);
                break;
            case "failed":
                Hold(null, FetchStatus.Unreachable);
                break;
        }

        return Task.CompletedTask;
    }
}
#endif
