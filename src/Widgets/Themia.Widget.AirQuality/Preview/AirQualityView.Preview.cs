#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2052]): <c>sample</c> is the index's sample (<see cref="ShowSampleAsync"/>);
/// <c>poor</c> a bad day's readings. <c>stale</c> is the sample kept through a failed refresh, <c>failed</c>
/// no answer at all.
/// </summary>
internal sealed partial class AirQualityView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "poor", "stale", "failed"];

    public Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "sample":
                return ShowSampleAsync();
            case "poor":
                HoldSample(SampleReading(poor: true), FetchStatus.Ok);
                break;
            case "stale":
                // [S-7042] The answer on screen, and the last refresh failed: the header's problem glyph.
                HoldSample(SampleReading(poor: false).Stale(FetchStatus.Offline, null), FetchStatus.Ok);
                break;
            case "failed":
                // [S-6734] failed: no answer, and the service out of reach: the Status row.
                HoldSample(null, FetchStatus.Unreachable);
                break;
        }

        return Task.CompletedTask;
    }
}
#endif
