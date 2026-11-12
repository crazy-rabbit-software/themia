using Themia.App.Services.Web;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;
using Themia.Widget.StockHistory;

namespace Themia.App.Widgets;

/// <summary>[S-11850] [S-1917] The chart's sample: the SDK's sample quote (<see cref="SampleQuote"/>), the price atom's, rising, its high and low marked.</summary>
internal sealed partial class StockHistoryView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        _feed.Preview(SampleConfig(rising: true), FetchStatus.Ok);
        return Task.CompletedTask;
    }

    /// <summary>[S-2448] The card's own config with the sample symbol; the sample quote, rising or falling.</summary>
    private Fetched<TickerQuote> SampleConfig(bool rising)
    {
        var sample = (StockHistoryConfig)_config.Clone();
        sample.Symbol = "MSFT";
        sample.ShowMinMax = true;
        _config = sample;
        return SampleQuote.Quote(rising);
    }
}
