#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.AirQualityGraph;

/// <summary>
/// Debug renders only ([S-11852]): <c>sample</c> the sample days; <c>smog</c> a smoggy spell (the PM2.5 bands up to
/// Critical); <c>stale</c> the sample kept through a failed refresh; <c>loading</c>, <c>empty</c> and <c>failed</c>
/// the Status row.
/// </summary>
internal sealed partial class AirQualityGraphView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "smog", "stale", "loading", "empty", "failed"];

    public Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "sample":
                return ShowSampleAsync();
            case "smog":
                // Four times the sample's particles: Moderate by the afternoon, Poor and Very poor on the third day.
                Hold(new AirHours([.. SampleHours.Air().Hours.Select(h => h with { Pm25 = h.Pm25 * 4, Pm10 = h.Pm10 * 4, EuropeanAqi = h.EuropeanAqi * 3, UsAqi = h.UsAqi * 3 })]), FetchStatus.Ok);
                break;
            case "stale":
                Hold(SampleHours.Air(), FetchStatus.Ok, stale: true);
                break;
            case "loading":
                Hold(null, FetchStatus.Loading);
                break;
            case "empty":
                Hold(new AirHours([new AirHour(SampleDay.HoursAgo(30)) { Pm25 = 5 }]), FetchStatus.Ok);
                break;
            case "failed":
                Hold(null, FetchStatus.Unreachable);
                break;
        }

        return Task.CompletedTask;
    }
}
#endif
