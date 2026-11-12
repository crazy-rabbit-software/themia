using Themia.App.Services.Web;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-1917] The quote's sample: the SDK's sample quote (<see cref="SampleQuote"/>, the chart's too),
/// rising, ten shares held, so the price and the holding's row ([S-2777]) draw without a symbol, the
/// network or the sync service. Nothing is fetched or saved. Its bars are the last session before the
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
    /// [S-2448] The card's own config (its style, its size) with the sample symbol, ten shares held when
    /// <paramref name="rising"/>; the sample quote that goes with it.
    /// </summary>
    private Fetched<TickerQuote> SampleConfig(bool rising)
    {
        var sample = (StockConfig)_config.Clone();
        sample.Symbol = "MSFT";
        sample.SharesOwned = rising ? 10 : null;
        _config = sample;
        return SampleQuote.Quote(rising);
    }
}
