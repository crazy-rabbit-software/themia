#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): the SDK's sample quote (<see cref="SampleQuote"/>, the chart's too), so the price can be looked at without a
/// symbol, the network or the sync service. Nothing is fetched or saved. Its bars are the last
/// session before the sample moment, a seeded walk ([S-2050], [S-2056]).
/// </summary>
internal sealed partial class StockView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["quote", "falling", "stale"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state is not ("quote" or "falling" or "stale"))
        {
            return Task.CompletedTask;
        }

        // [S-2777] "quote" and "stale" hold ten shares, so the holding's row can be looked at; "falling" none.
        var rising = state != "falling";
        _config = new StockConfig { Symbol = "MSFT", SharesOwned = rising ? 10 : null };
        var quote = SampleQuote.Quote(rising);

        // [S-7042] stale: the answer on screen, and the last refresh failed: the header's problem glyph.
        _feed.Preview(state == "stale" ? quote.Stale(FetchStatus.Offline, null) : quote, FetchStatus.Ok);
        return Task.CompletedTask;
    }
}
#endif
