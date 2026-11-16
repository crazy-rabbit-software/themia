using Themia.App.Services.Web;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-1917] The quote's sample: the SDK's sample quote (<see cref="SampleQuote"/>, the chart's too),
/// rising, so the price (and the holding's row where the card's config holds shares, [S-2777]) draw without a
/// symbol, the network or the sync service. [S-11602] The card's own rows: the sample never adds the holding's
/// row the box was not sized for (it stood half cut off under the price in a box a cell taller). Nothing is fetched or saved. Its bars are the last session before the
/// sample moment, a seeded walk ([S-2050], [S-2056]).
/// </summary>
internal sealed partial class StockView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        _feed.Preview(SampleConfig(rising: true), FetchStatus.Ok);
        return Task.CompletedTask;
    }

    /// <summary>
    /// [S-2448] [S-11602] The card's own config (its style, its rows, its holding) with the sample symbol; the
    /// sample quote that goes with it, rising or falling.
    /// </summary>
    private Fetched<TickerQuote> SampleConfig(bool rising)
    {
        var sample = (StockConfig)_config.Clone();
        sample.Symbol = "MSFT";
        _config = sample;
        return SampleQuote.Quote(rising);
    }
}
