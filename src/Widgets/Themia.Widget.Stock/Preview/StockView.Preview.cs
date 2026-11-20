using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): <c>quote</c> is the sample (<see cref="ShowSampleAsync"/>); <c>falling</c>
/// the falling session; <c>stale</c> the sample through a failed refresh; <c>held</c> the sample with ten
/// shares held, its holding's row counted in the box it is drawn at ([S-11603]).
/// </summary>
internal sealed partial class StockView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["quote", "falling", "stale", "held"];

    /// <inheritdoc />
    public Themia.Core.Model.WidgetConfig? DrawnConfig { get; private set; }

    public Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "held":
                var quote = SampleConfig(rising: true);
                _config.SharesOwned = 10;
                DrawnConfig = _config;
                _feed.Preview(quote, FetchStatus.Ok);
                break;

            case "quote":
                return ShowSampleAsync();

            case "falling":
                _feed.Preview(SampleConfig(rising: false), FetchStatus.Ok);
                break;

            case "stale":
                // [S-7042] The answer on screen, and the last refresh failed: the status's problem glyph.
                _feed.Preview(SampleConfig(rising: true).Stale(FetchStatus.Offline, null), FetchStatus.Ok);
                break;
        }

        return Task.CompletedTask;
    }
}
