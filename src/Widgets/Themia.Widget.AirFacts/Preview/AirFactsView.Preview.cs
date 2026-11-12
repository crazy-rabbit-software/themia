#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.Widget.AirFacts;

/// <summary>
/// Debug renders only ([S-1917]): <c>sample</c> is the air index's sample (<see cref="ShowSampleAsync"/>);
/// <c>poor</c> a bad day's readings; <c>stale</c> is the sample through a failed refresh, <c>failed</c> no
/// answer at all.
/// </summary>
internal sealed partial class AirFactsView : IWidgetPreviewState
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
                HoldSample(SampleReading(poor: false).Stale(FetchStatus.Offline, null), FetchStatus.Ok);
                break;
            case "failed":
                HoldSample(null, FetchStatus.Unreachable);
                break;
        }

        return Task.CompletedTask;
    }
}
#endif
