#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.ExchangeHistory;

/// <summary>Debug renders only ([S-2056]): <c>sample</c> is the rate history's sample (<see cref="ShowSampleAsync"/>); <c>stale</c> the same through a failed refresh.</summary>
internal sealed partial class ExchangeHistoryView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "stale"];

    public Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "sample":
                return ShowSampleAsync();
            case "stale":
                HoldSample(SampleRates.Table().Stale(FetchStatus.Offline, null));
                break;
        }

        return Task.CompletedTask;
    }
}
#endif
