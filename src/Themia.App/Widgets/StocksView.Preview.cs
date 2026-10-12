#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): a fixture quote, so the ladder can be looked at without a
/// symbol, the network or the sync service. Nothing is fetched or saved.
/// </summary>
internal sealed partial class StocksView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["quote", "falling"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state is not ("quote" or "falling"))
        {
            return Task.CompletedTask;
        }

        var rising = state == "quote";
        var start = DateTimeOffset.UtcNow.AddHours(-7).ToUnixTimeSeconds();
        var bars = new List<QuoteBar>();

        for (var i = 0; i < 78; i++)
        {
            var wave = (Math.Sin(i / 6.0) * 2.4) + (Math.Sin(i / 2.3) * 0.8);
            var drift = (rising ? 0.09 : -0.09) * i;
            bars.Add(new QuoteBar(start + (i * 300), 412 + wave + drift));
        }

        // [S-2775] The change and the line on one baseline, as the service builds them.
        var trace = MarketQuote.Trace(bars, bars[^1].Close, null);
        var quote = new TickerQuote("MSFT", "Microsoft Corporation", bars[^1].Close, trace.ChangePercent, "USD", 2, trace.Bars, TradingSessions.None);

        // [S-2777] "quote" holds ten shares, so the holding's caption can be looked at; "falling" none.
        _config = new StocksConfig { Symbol = "MSFT", ShowMinMax = true, SharesOwned = rising ? 10 : null };
        _applied = (StocksConfig)_config.Clone();
        _want = null;
        _quote = new Fetched<TickerQuote>(FetchStatus.Ok, quote, null, DateTimeOffset.Now);
        PublishTitle();
        Render();
        return Task.CompletedTask;
    }
}
#endif
