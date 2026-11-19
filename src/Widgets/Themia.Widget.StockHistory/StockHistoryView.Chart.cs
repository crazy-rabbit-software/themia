using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1311] The chart and what is written on it, on the one chart ([S-6717], [S-6750]) in its look ([S-6752]): the
/// period's line at 2 with its wash, alone, or its candles (the accent, Critical while the price falls), the bars outside the regular session as
/// the second series in the tertiary role ([S-1308]), the optional high and low markers [S-1310], the
/// start and end times in Caption tertiary inside its bottom band [S-1309], and the pointer readout.
/// Edge to edge and down to the card's foot, whose rounded corners clip it, the inset over it.
/// </summary>
internal sealed partial class StockHistoryView
{
    private readonly Grid _chart = new();
    private readonly Chart _series = new();
    private readonly Canvas _overlay = new() { IsHitTestVisible = false };
    private readonly Line _rule = new() { StrokeThickness = FamilyShape.Hairline, Visibility = Visibility.Collapsed };
    private readonly Border _probe = new() { Visibility = Visibility.Collapsed, Padding = new Thickness(FamilySpace.S1, 0, FamilySpace.S1, 0) };
    private readonly TextBlock _probeText = Family.Text(FamilyType.Caption);

    private readonly Grid _labels = new() { IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Bottom };
    private readonly TextBlock _startTime = Family.Text(FamilyType.Caption, ColourRole.TextTertiary);
    private readonly TextBlock _endTime = Family.Text(FamilyType.Caption, ColourRole.TextTertiary);

    /// <summary>Where each bar was drawn, for the markers and the pointer readout.</summary>
    private readonly List<ChartPoint> _drawn = [];

    private void BuildChart()
    {
        _rule.Stroke = Family.Brush(ColourRole.TextTertiary);
        _probe.Child = _probeText;
        _probe.CornerRadius = Family.Radius;
        _probe.Background = Family.Brush(ColourRole.Solid);
        _overlay.Children.Add(_rule);
        _overlay.Children.Add(_probe);

        _chart.Children.Add(_series);
        _chart.Children.Add(_labels);
        _chart.Children.Add(_overlay);

        // The chart's own surface takes the pointer for the readout; the rest of the card is still
        // the card's to drag. A clear fill is what makes an empty area hit-testable.
        // [S-1113] A price over time: the time axis runs left to right in every language.
        _chart.FlowDirection = FlowDirection.LeftToRight;
        _chart.Background = Family.Brush(ColourRole.Clear);
        _chart.PointerMoved += OnChartPointerMoved;
        _chart.PointerExited += OnChartPointerGone;
        _chart.PointerCanceled += OnChartPointerGone;

        // [S-1311] The start and end times, over the wash in the chart's bottom band, at the text's
        // inset; the trace's band keeps clear of them (StockChartLabels.Band).
        _endTime.HorizontalAlignment = HorizontalAlignment.Right;
        _startTime.HorizontalAlignment = HorizontalAlignment.Left;
        _labels.Children.Add(_startTime);
        _labels.Children.Add(_endTime);
        _labels.Margin = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, StockChartLabels.TimesBottom);
    }

    /// <summary>[S-1309] [S-1311] The start and end times, only those, in the exchange's zone.</summary>
    private void UpdateLabels(TickerQuote quote)
    {
        var labels = ExchangeTime.Labels(quote.Bars, quote.ExchangeOffset, _config.Range, DateTimeOffset.Now.Offset);

        _startTime.Text = labels.Count > 0 ? labels[0] : string.Empty;
        _endTime.Text = labels.Count > 0 ? labels[^1] : string.Empty;

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
        _series.Role(_falling ? ColourRole.Critical : ColourRole.Accent);
        _labels.Visibility = quote is not null && ChartLooks.EffectiveDetail(_config) == ChartDetail.Simple ? Visibility.Visible : Visibility.Collapsed;
        _series.Show(size => Layout(quote, size.Width, caption));
        MarkInstant();
    }

    /// <summary>[S-1308] [S-1310] [S-1311] [S-6751] What the chart draws across a band <paramref name="width"/> wide.</summary>
    private ChartModel Layout(TickerQuote? quote, double width, string caption)
    {
        _drawn.Clear();
        var type = ChartLooks.Effective(_config);
        var detail = ChartLooks.EffectiveDetail(_config);

        // [S-1310] [S-1311] The trace keeps clear of what is written on the chart: the high/low prices above and
        // below it, the times under that. The wash still reaches the bottom edge.
        var markers = _config.ShowMinMax && detail == ChartDetail.Simple;
        var line = FamilyType.CaptionLine;
        var model = new ChartModel
        {
            Type = type,
            Detail = detail,
            PadTop = markers ? line : 0,
            PadBottom = (markers ? line : 0) + line + StockChartLabels.TimesBottom,
            Caption = caption,
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

    /// <summary>[S-1311] On an expanded width, the pointer over the chart reads the bar under it: time and price.</summary>
    private void OnChartPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (_drawn.Count < 2 || _quote?.Value is not { } quote)
        {
            return;
        }

        var x = args.GetCurrentPoint(_chart).Position.X;
        var width = _chart.ActualWidth;
        var index = (int)Math.Round(Math.Clamp(x / Math.Max(1, width), 0, 1) * (_drawn.Count - 1));
        var point = _drawn[index];
        var bar = quote.Bars[index];
        PublishInstant(bar.Time);

        _rule.X1 = _rule.X2 = point.X;
        _rule.Y1 = 0;
        _rule.Y2 = _chart.ActualHeight;
        _rule.Visibility = Visibility.Visible;

        var range = _config.Range is QuoteRange.Day or QuoteRange.FiveDays ? QuoteRange.FiveDays : QuoteRange.Year;
        _probeText.Text = $"{ExchangeTime.Label(bar.Time, quote.ExchangeOffset, range, DateTimeOffset.Now.Offset)} · {MarketQuote.Price(bar.Close, quote.PriceHint)}";
        _probe.Visibility = Visibility.Visible;

        var estimate = (HeroFit.Ems(_probeText.Text) * FamilyType.Caption.FontSize) + (2 * FamilySpace.S1);
        Canvas.SetLeft(_probe, Math.Clamp(point.X - (estimate / 2), 0, Math.Max(0, width - estimate)));
        Canvas.SetTop(_probe, 0);
    }

    private void OnChartPointerGone(object sender, PointerRoutedEventArgs args)
    {
        HideProbe();
        UnpublishInstant();
        MarkInstant();
    }

    private void HideProbe()
    {
        _rule.Visibility = Visibility.Collapsed;
        _probe.Visibility = Visibility.Collapsed;
    }

    private void DetachChart()
    {
        _drawn.Clear();
        HideProbe();
    }
}
