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
/// [S-1311] The chart and what is written on it, on the Chart block ([S-6717]): the period's line at 2
/// with its wash (the accent, Critical while the price falls), the bars outside the regular session as
/// the second series in the tertiary role ([S-1308]), the optional high and low markers [S-1310], the
/// start and end times in Caption tertiary inside its bottom band [S-1309], and the pointer readout.
/// Edge to edge and down to the card's foot, whose rounded corners clip it, the inset over it.
/// </summary>
internal sealed partial class StockHistoryView
{
    /// <summary>[S-1310] A marker's dot: the line's stroke either side of the track, so it reads on the 2 px line.</summary>
    private const double MarkerDot = FamilyShape.Track + FamilyShape.Stroke;

    private readonly Grid _chart = new();
    private readonly ChartSeries _series = new();
    private readonly Canvas _canvas = new() { IsHitTestVisible = false };
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
        _chart.Children.Add(_canvas);
        _chart.Children.Add(_labels);
        _chart.Children.Add(_overlay);

        // The chart's own surface takes the pointer for the readout; the rest of the card is still
        // the card's to drag. A clear fill is what makes an empty area hit-testable.
        // [S-1113] A price over time: the time axis runs left to right in every language.
        _chart.FlowDirection = FlowDirection.LeftToRight;
        _chart.Background = Family.Brush(ColourRole.Clear);
        _chart.SizeChanged += OnChartSizeChanged;
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

    private void OnChartSizeChanged(object sender, SizeChangedEventArgs args) => RedrawChart();

    /// <summary>
    /// Redraw the line on its band. Cheap by construction: a handful of shapes, rebuilt only on a new
    /// quote or a resize, over a series the service already downsampled to a few hundred points.
    /// </summary>
    private void RedrawChart()
    {
        _canvas.Children.Clear();
        _drawn.Clear();

        if (_chart.Visibility != Visibility.Visible || _quote?.Value is not { } quote)
        {
            return;
        }

        var width = _chart.ActualWidth;
        var height = _chart.ActualHeight;
        var bars = quote.Bars;
        if (width <= 0 || height <= 0 || bars.Count < 2)
        {
            _series.Show(Array.Empty<ChartPoint>(), null);
            return;
        }

        var role = _falling ? ColourRole.Critical : ColourRole.Accent;
        _series.Role(role);

        // [S-1310] [S-1311] The trace keeps clear of what is written on the chart: the high/low
        // prices above and below it, the times under that. The wash still reaches the bottom edge.
        var band = StockChartLabels.Band(height, _config.ShowMinMax, true);
        var scaled = MarketQuote.Normalise(MarketQuote.Closes(bars));
        for (var i = 0; i < scaled.Count; i++)
        {
            _drawn.Add(new ChartPoint(i * width / (scaled.Count - 1), band.Top + ChartRules.Y(scaled[i], band.Usable)));
        }

        // [S-1308] The regular session is the line and its wash; the bars outside it the second series.
        var (from, to) = Regular(bars, quote.HasPrePost ? quote.Sessions : null);
        var regular = from == 0 && to == _drawn.Count ? _drawn : _drawn.GetRange(from, to - from);
        _series.Show(regular, regular.Count == _drawn.Count ? null : _drawn);

        if (_config.ShowMinMax && MarketQuote.Extremes(bars) is { } extremes)
        {
            var marked = new List<ChartMarker>(2)
            {
                new(_drawn[extremes.High].X, _drawn[extremes.High].Y, MarketQuote.Price(bars[extremes.High].Close, quote.PriceHint), Above: true),
                new(_drawn[extremes.Low].X, _drawn[extremes.Low].Y, MarketQuote.Price(bars[extremes.Low].Close, quote.PriceHint), Above: false),
            };

            var boxes = StockChartLabels.Place(width, marked);
            for (var i = 0; i < marked.Count; i++)
            {
                Marker(marked[i], boxes[i], role);
            }
        }
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

    /// <summary>[S-1310] A dot on the extreme in the line's role, with its price beside it (an axis label: Caption tertiary), kept inside the chart.</summary>
    private void Marker(ChartMarker marker, LabelBox box, ColourRole role)
    {
        var dot = new Ellipse { Width = MarkerDot, Height = MarkerDot, Fill = Family.Brush(role) };
        Canvas.SetLeft(dot, marker.X - (MarkerDot / 2));
        Canvas.SetTop(dot, marker.Y - (MarkerDot / 2));
        _canvas.Children.Add(dot);

        var label = Family.Text(FamilyType.Caption, ColourRole.TextTertiary, marker.Text);
        Canvas.SetLeft(label, box.Left);
        Canvas.SetTop(label, box.Top);
        _canvas.Children.Add(label);
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

    private void OnChartPointerGone(object sender, PointerRoutedEventArgs args) => HideProbe();

    private void HideProbe()
    {
        _rule.Visibility = Visibility.Collapsed;
        _probe.Visibility = Visibility.Collapsed;
    }

    private void DetachChart()
    {
        _canvas.Children.Clear();
        _drawn.Clear();
        HideProbe();
    }
}
