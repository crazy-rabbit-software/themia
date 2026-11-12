#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>Debug renders only ([S-1917]): <c>quote</c> is the sample (<see cref="ShowSampleAsync"/>); <c>falling</c> the falling session; <c>stale</c> the sample through a failed refresh.</summary>
internal sealed partial class StockHistoryView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["quote", "falling", "stale"];

    public Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "quote":
                return ShowSampleAsync();

            case "falling":
                _feed.Preview(SampleConfig(rising: false), FetchStatus.Ok);
                break;

            case "stale":
                _feed.Preview(SampleConfig(rising: true).Stale(FetchStatus.Offline, null), FetchStatus.Ok);
                break;
        }

        return Task.CompletedTask;
    }
}
#endif
