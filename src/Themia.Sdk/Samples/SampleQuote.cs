#if DEBUG
using Themia.App.Services.Web;
using Themia.Core.Widgets;

namespace Themia.Sdk.Samples;

/// <summary>
/// Debug renders only ([S-2050], [S-2056]): the sample day's quote of MSFT, the last full session before
/// the sample moment (New York opens at 15:30 in Zürich, after 10:42): yesterday's, or Friday's, 9:30 to
/// 16:00 Eastern in five-minute bars, a seeded walk of small moves that ends up (or, falling, down) on
/// the day. The price and its chart atoms both read it, so a recipe's two agree ([S-11171]).
/// </summary>
public static class SampleQuote
{
    public static Fetched<TickerQuote> Quote(bool rising)
    {
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
        return new Fetched<TickerQuote>(FetchStatus.Ok, quote, null, SampleDay.Now);
    }
}
#endif
