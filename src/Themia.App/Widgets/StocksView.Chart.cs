using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// The chart and what sits around it: the trace (solid in session, dashed outside it [S-1308]),
/// the optional fill and high/low markers [S-1310], the price axis, the times under it in the
/// exchange's zone [S-1309] and the pointer readout [S-1311].
/// </summary>
internal sealed partial class StocksView
{
    private readonly Grid _chart = new() { MinHeight = StockLayout.MinChart };
    private readonly Canvas _canvas = new();
    private readonly Polygon _fill = new();
    private readonly Canvas _overlay = new() { IsHitTestVisible = false };
    private readonly Line _rule = new() { StrokeThickness = Fluent.Hairline, Visibility = Visibility.Collapsed };
    private readonly Border _probe = new() { Visibility = Visibility.Collapsed, Padding = new Thickness(Fluent.Space1, 0, Fluent.Space1, 0) };
    private readonly TextBlock _probeText = Fluent.Text(FluentType.Caption);

    private readonly Grid _labels = new();
    private readonly TextBlock[] _labelTexts = [
        Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush"),
        Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush"),
        Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush"),
    ];

    /// <summary>Where each bar was drawn, for the pointer readout.</summary>
    private readonly List<Point> _drawn = [];

    private void BuildChart()
    {
        _fill.Opacity = Fluent.ChartWash;
        _fill.IsHitTestVisible = false;
        _canvas.IsHitTestVisible = false;

        _rule.Stroke = FluentLayout.Themed("TextFillColorTertiaryBrush", Colors.Gray);
        _probe.Child = _probeText;
        _probe.CornerRadius = new CornerRadius(Fluent.ControlRadius);
        _probe.Background = FluentLayout.Themed("SolidBackgroundFillColorTertiaryBrush", Colors.Black);
        _overlay.Children.Add(_rule);
        _overlay.Children.Add(_probe);

        _chart.Children.Add(_canvas);
        _chart.Children.Add(_overlay);

        // The chart's own surface takes the pointer for the readout; the rest of the card is still
        // the card's to drag. A transparent fill is what makes an empty area hit-testable.
        _chart.Background = FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent);
        _chart.SizeChanged += OnChartSizeChanged;
        _chart.PointerMoved += OnChartPointerMoved;
        _chart.PointerExited += OnChartPointerGone;
        _chart.PointerCanceled += OnChartPointerGone;

        for (var i = 0; i < 3; i++)
        {
            _labels.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            SetColumn(_labelTexts[i], i);
            _labels.Children.Add(_labelTexts[i]);
        }

        _labelTexts[1].HorizontalAlignment = HorizontalAlignment.Center;
        _labelTexts[2].HorizontalAlignment = HorizontalAlignment.Right;
        _labels.Margin = new Thickness(StockLayout.Inset, StockLayout.Gap, StockLayout.Inset, Fluent.Space1);
    }

    /// <summary>[S-1309] Start and end (and middle) times under the chart, in the exchange's zone.</summary>
    private void UpdateLabels(TickerQuote quote, StockPlan plan)
    {
        var labels = ExchangeTime.Labels(quote.Bars, quote.ExchangeOffset, _config.Range, DateTimeOffset.Now.Offset, plan.MiddleLabel);

        _labelTexts[0].Text = labels.Count > 0 ? labels[0] : string.Empty;
        _labelTexts[1].Text = labels.Count == 3 ? labels[1] : string.Empty;
        _labelTexts[2].Text = labels.Count > 0 ? labels[^1] : string.Empty;

        ToolTipService.SetToolTip(_labels, ExchangeTime.ZoneNote(quote.ZoneName));
    }

    private void OnChartSizeChanged(object sender, SizeChangedEventArgs args) => RedrawChart();

    /// <summary>
    /// Redraw the trace. Cheap by construction: a handful of shapes, rebuilt only on a new quote or
    /// a resize, over a series the service already downsampled to a few hundred points.
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
            return;
        }

        var scaled = MarketQuote.Normalise(MarketQuote.Closes(bars));

        // Inset by the stroke width so the period's high and low are not clipped in half at the
        // edges, and by a caption line when the high/low labels need room above and below.
        var markers = _plan.MinMax;
        var pad = markers ? FluentType.Caption.LineHeight : Fluent.ChartStroke;
        var usable = Math.Max(1, height - (2 * pad));

        for (var i = 0; i < scaled.Count; i++)
        {
            // The window is exactly as long as the series, which spreads the points edge to edge.
            var x = Sparkline.SampleX(i, scaled.Count, scaled.Count) * width;
            var y = pad + (Sparkline.SampleY(scaled[i]) * usable);
            _drawn.Add(new Point(x, y));
        }

        if (_config.ShowAreaFill)
        {
            var fill = new PointCollection { new(_drawn[0].X, height) };

            foreach (var point in _drawn)
            {
                fill.Add(point);
            }

            fill.Add(new Point(_drawn[^1].X, height));
            _fill.Points = fill;
            _fill.Fill = _direction;
            _canvas.Children.Add(_fill);
        }

        // [S-1308] Solid inside the regular session, dashed and fainter outside it.
        foreach (var (from, to, extended) in MarketQuote.Segments(bars, quote.HasPrePost ? quote.Sessions : null))
        {
            var line = new Polyline
            {
                Stroke = _direction,
                StrokeThickness = Fluent.ChartStroke,
                StrokeLineJoin = PenLineJoin.Round,
                Opacity = extended ? 0.55 : 1,
                Points = [],
            };

            if (extended)
            {
                line.StrokeDashArray = [2, 2];
            }

            // Each run starts at the last point of the one before, so the line has no gap.
            for (var i = Math.Max(0, from - 1); i < to; i++)
            {
                line.Points.Add(_drawn[i]);
            }

            _canvas.Children.Add(line);
        }

        var marked = new List<ChartMarker>(2);

        if (markers && MarketQuote.Extremes(bars) is { } extremes)
        {
            marked.Add(new ChartMarker(_drawn[extremes.High].X, _drawn[extremes.High].Y, MarketQuote.Price(bars[extremes.High].Close, quote.PriceHint), Above: true));
            marked.Add(new ChartMarker(_drawn[extremes.Low].X, _drawn[extremes.Low].Y, MarketQuote.Price(bars[extremes.Low].Close, quote.PriceHint), Above: false));
        }

        string[] axis = [];

        if (_plan.Axis)
        {
            var closes = MarketQuote.Closes(bars);
            var low = closes.Min();
            var high = closes.Max();
            axis = [MarketQuote.Price(high, quote.PriceHint), MarketQuote.Price((high + low) / 2, quote.PriceHint), MarketQuote.Price(low, quote.PriceHint)];
        }

        // [S-1310] An axis label a marker's price would overlap is left out ([S-1311]).
        var labels = StockChartLabels.Place(width, height, pad, usable, marked, axis);

        for (var i = 0; i < marked.Count; i++)
        {
            Marker(marked[i], labels.Markers[i]);
        }

        for (var i = 0; i < axis.Length; i++)
        {
            if (labels.Axis[i] is { } box)
            {
                AxisLabel(axis[i], box, width);
            }
        }
    }

    /// <summary>[S-1310] A dot on the extreme with its price beside it, kept inside the chart.</summary>
    private void Marker(ChartMarker marker, LabelBox box)
    {
        const double Dot = 6;
        var dot = new Ellipse { Width = Dot, Height = Dot, Fill = _direction };
        Canvas.SetLeft(dot, marker.X - (Dot / 2));
        Canvas.SetTop(dot, marker.Y - (Dot / 2));
        _canvas.Children.Add(dot);

        var label = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        label.Text = marker.Text;
        Canvas.SetLeft(label, box.Left);
        Canvas.SetTop(label, box.Top);
        _canvas.Children.Add(label);
    }

    /// <summary>[S-1311] One price of the axis (high, middle or low) at the chart's right edge.</summary>
    private void AxisLabel(string value, LabelBox box, double width)
    {
        const double Box = 96;
        var text = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
        text.Text = value;
        text.TextAlignment = TextAlignment.Right;
        text.Width = Box;
        Canvas.SetLeft(text, width - Box - StockLayout.Inset);
        Canvas.SetTop(text, box.Top);
        _canvas.Children.Add(text);
    }

    /// <summary>[S-1311] On a large tile, the pointer over the chart reads the bar under it: time and price.</summary>
    private void OnChartPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (_drawn.Count < 2 || _quote?.Value is not { } quote || _area.WidthClass < SizeClass.Expanded || _plan.Beside)
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

        var estimate = FluentType.EstimateWidth(FluentType.Caption, _probeText.Text.Length) + (2 * Fluent.Space1);
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
