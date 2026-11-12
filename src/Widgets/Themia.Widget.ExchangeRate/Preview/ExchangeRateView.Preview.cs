#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>Debug renders only ([S-1917], [S-2056]): <c>sample</c> is the rate's sample (<see cref="ShowSampleAsync"/>); <c>stale</c> the same through a failed refresh.</summary>
internal sealed partial class ExchangeRateView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "stale"];

    public Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "sample":
                return ShowSampleAsync();
            case "stale":
                // [S-7042] The answer on screen, and the last refresh failed: the header's problem glyph.
                HoldSample(SampleRates.Table().Stale(FetchStatus.Offline, null));
                break;
        }

        return Task.CompletedTask;
    }
}
#endif
