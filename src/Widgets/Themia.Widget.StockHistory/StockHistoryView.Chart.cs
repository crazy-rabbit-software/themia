using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Links;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.StockHistory;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1311] The chart and what is written on it, on the one chart ([S-6717], [S-6750]) in its look ([S-6752]): the
/// period's line at 2 with its wash, alone, or its candles (the accent, Critical while the price falls), the bars outside the regular session as
/// the second series in the tertiary role ([S-1308]), the optional high and low markers [S-1310], the
/// start and end times the chart writes at its foot [S-1309], and the pointer readout. The card's edge to edge, its top
/// too, whose rounded corners clip it ([S-6754]).
/// </summary>
internal sealed partial class StockHistoryView
{
    /// <summary>[S-6756] The price panel: the chart, its hairline and the pointer's readout, its moments on the view's links.</summary>
    private readonly PriceChart _chart = new();

    /// <summary>[S-1309] The period's start and end times, in the exchange's zone; written by the chart at its foot.</summary>
    private ChartEnds _ends = new(string.Empty, string.Empty);

    /// <summary>Where each bar was drawn, for the markers and the pointer readout.</summary>
    private readonly List<ChartPoint> _drawn = [];

    /// <summary>[S-1311] [S-11910] The bar under the pointer published and read out (time and price); a moment pointed at elsewhere marked.</summary>
    private void BuildChart()
        => _chart.Moments(
            _links,
            LinkTopics.HoveredInstant,
            () => Subject,
            () => _quote?.Value is { } quote && _drawn.Count == quote.Bars.Count ? _drawn.Count : 0,
            i => DateTimeOffset.FromUnixTimeSeconds(_quote!.Value!.Bars[i].Time),
            BarAt,
            Readout);

    /// <summary>[S-1309] [S-1311] The start and end times, only those, in the exchange's zone.</summary>
    private void UpdateLabels(TickerQuote quote)
    {
        var labels = ExchangeTime.Labels(quote.Bars, quote.ExchangeOffset, _config.Range, DateTimeOffset.Now.Offset);

        _ends = new ChartEnds(labels.Count > 0 ? labels[0] : string.Empty, labels.Count > 0 ? labels[^1] : string.Empty);

        // The labels take no pointer (the chart's readout does), so the zone is the chart's tooltip.
        ToolTipService.SetToolTip(_chart, ExchangeTime.ZoneNote(quote.ZoneName));
    }

    /// <summary>
    /// [S-6750] Draw the period on the one chart in the config's look. Cheap by construction: a handful of shapes,
    /// rebuilt only on a new quote or a resize, over a series the service already downsampled to a few hundred points.
    /// [S-6753] With no quote, the chart at its baseline with why as its caption.
    /// </summary>
    private void RedrawChart(string caption = "")
    {
        var quote = _quote?.Value is { Bars.Count: >= 2 } held ? held : null;
        _chart.Series.Role(_falling ? ColourRole.Critical : ColourRole.Accent);
        _chart.Series.Show(size => Layout(quote, size.Width, caption));
        _chart.Mark();
    }

    /// <summary>[S-1308] [S-1310] [S-1311] [S-6751] What the chart draws across a band <paramref name="width"/> wide.</summary>
    private ChartModel Layout(TickerQuote? quote, double width, string caption)
    {
        _drawn.Clear();
        var type = ChartLooks.Effective(_config);
        var detail = ChartLooks.EffectiveDetail(_config);

        // [S-1310] [S-1311] [S-6754] The trace keeps clear of what is written on the chart: the high/low prices above and
        // below it, the times under that. The wash still reaches the bottom edge.
        var markers = _config.ShowMinMax && detail == ChartDetail.Simple;
        var model = StockHistoryModule.Frame(_config) with
        {
            Caption = caption,
            Ends = detail == ChartDetail.Simple ? (quote is null ? new ChartEnds(string.Empty, string.Empty) : _ends) : null,
        };

        if (quote is null)
        {
            return model;
        }

        var bars = quote.Bars;
        for (var i = 0; i < bars.Count; i++)
        {
            _drawn.Add(new ChartPoint(i * width / (bars.Count - 1), 0));
        }

        var all = bars.Select((bar, i) => new ChartSample(_drawn[i].X, bar.Close)).ToList();

        // [S-1308] The regular session is the line and its wash; the bars outside it the second series.
        var (from, to) = Regular(bars, quote.HasPrePost ? quote.Sessions : null);
        var regular = from == 0 && to == all.Count ? all : all.GetRange(from, to - from);
        var closes = MarketQuote.Closes(bars);
        var marks = new List<ChartMark>(2);
        if (markers && MarketQuote.Extremes(bars) is { } extremes)
        {
            marks.Add(new(_drawn[extremes.High].X, bars[extremes.High].Close, MarketQuote.Price(bars[extremes.High].Close, quote.PriceHint), Under: false, ColourRole.TextTertiary, Dot: type != ChartType.Candles));
            marks.Add(new(_drawn[extremes.Low].X, bars[extremes.Low].Close, MarketQuote.Price(bars[extremes.Low].Close, quote.PriceHint), Under: true, ColourRole.TextTertiary, Dot: type != ChartType.Candles));
        }

        return model with
        {
            Series = type == ChartType.Candles ? all : regular,
            Second = type == ChartType.Candles || regular.Count == all.Count ? [] : all,
            Ohlc = bars.All(b => b.Ohlc is not null) ? [.. bars.Select(b => b.Ohlc!.Value)] : null,
            Low = closes.Min(),
            High = closes.Max(),
            Marks = marks,
            Format = v => MarketQuote.Price(v, quote.PriceHint),
            Times = detail == ChartDetail.Detailed ? Times(quote, width) : [],
        };
    }

    /// <summary>[S-6751] A Detailed chart's times: bars evenly apart across the band (the chart is by bar, not by clock), each in the exchange's zone.</summary>
    private IReadOnlyList<ChartTick> Times(TickerQuote quote, double width)
    {
        var bars = quote.Bars;
        var count = Math.Min(bars.Count, ChartPlot.TimeRoom(width));
        var ticks = new List<ChartTick>(count);
        for (var k = 0; k < count; k++)
        {
            var i = (int)Math.Round(k * (bars.Count - 1) / (double)Math.Max(1, count - 1));
            ticks.Add(new ChartTick(_drawn[i].X, ExchangeTime.Label(bars[i].Time, quote.ExchangeOffset, _config.Range, DateTimeOffset.Now.Offset)));
        }

        return ticks;
    }

    /// <summary>[S-1308] The run of bars in the regular session (all of them without sessions to tell).</summary>
    private static (int From, int To) Regular(IReadOnlyList<QuoteBar> bars, TradingSessions? sessions)
    {
        var from = -1;
        var to = 0;
        foreach (var (start, end, extended) in MarketQuote.Segments(bars, sessions))
        {
            if (!extended)
            {
                from = from < 0 ? Math.Max(0, start - 1) : from;
                to = end;
            }
        }

        return from < 0 ? (0, bars.Count) : (from, Math.Max(from + 1, to));
    }

    /// <summary>[S-1311] The pointer's readout of bar <paramref name="index"/>: its time and its price.</summary>
    private string Readout(int index)
    {
        var quote = _quote!.Value!;
        var bar = quote.Bars[index];
        var range = _config.Range is QuoteRange.Day or QuoteRange.FiveDays ? QuoteRange.FiveDays : QuoteRange.Year;
        return $"{ExchangeTime.Label(bar.Time, quote.ExchangeOffset, range, DateTimeOffset.Now.Offset)} · {MarketQuote.Price(bar.Close, quote.PriceHint)}";
    }

    private void DetachChart()
    {
        _drawn.Clear();
        _chart.Clear();
    }
}
