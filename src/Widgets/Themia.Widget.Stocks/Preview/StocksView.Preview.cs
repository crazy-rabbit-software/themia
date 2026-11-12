#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): a fixture quote, so the ladder can be looked at without a
/// symbol, the network or the sync service. Nothing is fetched or saved. Its bars are the last
/// session before the sample moment, a seeded walk ([S-2050], [S-2056]).
/// </summary>
internal sealed partial class StocksView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["quote", "falling", "stale"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state is not ("quote" or "falling" or "stale"))
        {
            return Task.CompletedTask;
        }

        // [S-2056] The last full session before the sample moment (New York opens at 15:30 in Zürich,
        // after 10:42): yesterday's, or Friday's, 9:30 to 16:00 Eastern in five-minute bars, a seeded
        // walk of small moves that ends up (or, falling, down) on the day.
        var rising = state != "falling";
        var session = SampleDay.Today.AddDays(-1);
        while (session.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            session = session.AddDays(-1);
        }

        var eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var start = SampleDay.At(session, new TimeOnly(9, 30), eastern).ToUnixTimeSeconds();
        var walk = SampleSeries.Walk(rising ? "msft-up" : "msft-down", 78, rising ? 418.62 : 405.37, 0.0011);
        var open = rising ? 412.40 : 411.85;
        var bars = new List<QuoteBar>();

        for (var i = 0; i < walk.Count; i++)
        {
            // The walk, leaned from the open to the close so the day's change has its sign.
            var lean = (open - walk[0]) * (1 - (i / (double)(walk.Count - 1)));
            bars.Add(new QuoteBar(start + (i * 300), Math.Round(walk[i] + lean, 2)));
        }

        // [S-2775] The change and the line on one baseline, as the service builds them.
        var trace = MarketQuote.Trace(bars, bars[^1].Close, null);
        var quote = new TickerQuote("MSFT", "Microsoft Corporation", bars[^1].Close, trace.ChangePercent, "USD", 2, trace.Bars, TradingSessions.None);

        // [S-2777] "quote" and "stale" hold ten shares, so the holding's row can be looked at; "falling" none.
        _config = new StocksConfig { Symbol = "MSFT", ShowMinMax = true, SharesOwned = rising ? 10 : null, Style = _config.Style };
        _applied = (StocksConfig)_config.Clone();
        _want = null;
        _quote = new Fetched<TickerQuote>(FetchStatus.Ok, quote, null, SampleDay.Now);
        if (state == "stale")
        {
            // [S-7042] The answer on screen, and the last refresh failed: the header's problem glyph.
            _quote = _quote.Stale(FetchStatus.Offline, null);
        }
        PublishTitle();
        Render();
        return Task.CompletedTask;
    }
}
#endif
