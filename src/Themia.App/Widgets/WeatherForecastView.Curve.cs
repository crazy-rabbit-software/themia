using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell;
using Themia.Core.Widgets;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>[S-6164] The forecast's temperature-curve style.</summary>
internal sealed partial class WeatherForecastView
{
    private const double CurveDot = 6;

    private readonly Brush _highBrush = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue);
    private readonly Brush _lowBrush = FluentLayout.Themed("TextFillColorTertiaryBrush", Colors.Gray);

    /// <summary>
    /// The days as columns (label, glyph, precipitation) over one chart that spans them: the highs as
    /// a smooth curve with each day's figure over its point, and with the height the lows under them
    /// on the same scale. Time runs left to right in every language ([S-1113]).
    /// </summary>
    private Grid BuildCurve(IReadOnlyList<ForecastDay> days, DateOnly today, ForecastCurvePlan plan)
    {
        var shown = days.Take(plan.Days).ToList();
        var grid = new Grid
        {
            Padding = new Thickness(ForecastCurve.Padding),
            RowSpacing = ForecastCurve.Gap,
            FlowDirection = FlowDirection.LeftToRight,
        };

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        if (plan.Icon)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        var chartRow = grid.RowDefinitions.Count;
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        if (plan.Precipitation)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(FluentType.Caption.LineHeight) });
        }

        for (var i = 0; i < shown.Count; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var day = shown[i];

            // [S-727] Today's quiet fill under its whole column, behind the chart.
            if (day.Day == today)
            {
                var fill = new Border { Background = _todayFill, CornerRadius = new CornerRadius(Fluent.ControlRadius) };
                SetColumn(fill, i);
                SetRowSpan(fill, grid.RowDefinitions.Count);
                grid.Children.Add(fill);
            }

            var label = DayText(day, today, FluentType.Caption);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            SetColumn(label, i);
            grid.Children.Add(label);

            if (plan.Icon)
            {
                var icon = Fluent.Icon(LookGlyph(WeatherReport.Look(day.Code)), "TextFillColorSecondaryBrush", PartMetrics.DayIcon);
                icon.HorizontalAlignment = HorizontalAlignment.Center;
                SetRow(icon, 1);
                SetColumn(icon, i);
                grid.Children.Add(icon);
            }

            if (plan.Precipitation)
            {
                var rain = Rain(day.Precipitation, withUnit: plan.ColumnWidth >= 72);
                rain.HorizontalAlignment = HorizontalAlignment.Center;
                SetRow(rain, chartRow + 1);
                SetColumn(rain, i);
                grid.Children.Add(rain);
            }

            // A hit target per column for the day's summary; the chart itself is not hit-testable.
            var hit = new Border { Background = new SolidColorBrush(Colors.Transparent) };
            ToolTipService.SetToolTip(hit, Describe(day));
            SetColumn(hit, i);
            SetRowSpan(hit, grid.RowDefinitions.Count);
            grid.Children.Add(hit);
        }

        var chart = Chart(shown, plan);
        SetRow(chart, chartRow);
        SetColumnSpan(chart, Math.Max(1, shown.Count));
        grid.Children.Add(chart);
        return grid;
    }

    /// <summary>The curves and their figures, drawn at the plan's size (known before layout, so no size handler).</summary>
    private Canvas Chart(List<ForecastDay> days, ForecastCurvePlan plan)
    {
        var canvas = new Canvas { IsHitTestVisible = false, Height = plan.ChartHeight, VerticalAlignment = VerticalAlignment.Top };
        var line = FluentType.Caption.LineHeight;
        var top = line + (CurveDot / 2);
        var bottom = plan.ChartHeight - (plan.Low ? line : 0) - (CurveDot / 2);

        var highs = days.Select(d => d.High).ToList();
        var lows = days.Select(d => d.Low).ToList();
        var (coldest, warmest) = Span(plan.Low ? highs.Concat(lows) : highs);

        Draw(canvas, ForecastCurve.Points(highs, coldest, warmest, plan.ColumnWidth, top, bottom), highs, _highBrush, above: true, plan.ColumnWidth);
        if (plan.Low)
        {
            Draw(canvas, ForecastCurve.Points(lows, coldest, warmest, plan.ColumnWidth, top, bottom), lows, _lowBrush, above: false, plan.ColumnWidth);
        }

        return canvas;
    }

    /// <summary>The range the curves share: the lows' and the highs' together, or the highs' alone without a low curve.</summary>
    private static (double Coldest, double Warmest) Span(IEnumerable<double?> values)
    {
        var finite = values.Where(v => v is { } x && double.IsFinite(x)).Select(v => v!.Value).ToList();
        return finite.Count == 0 ? (0, 1) : (finite.Min(), finite.Max());
    }

    /// <summary>One curve: the smooth runs, a dot on each day and the day's figure over (or under) it.</summary>
    private static void Draw(Canvas canvas, IReadOnlyList<CurvePoint?> points, List<double?> values, Brush brush, bool above, double columnWidth)
    {
        foreach (var run in ForecastCurve.Smooth(points))
        {
            if (run.Segments.Count == 0)
            {
                continue;
            }

            var figure = new PathFigure { StartPoint = new Point(run.Start.X, run.Start.Y), IsClosed = false, IsFilled = false };
            foreach (var segment in run.Segments)
            {
                figure.Segments.Add(new BezierSegment
                {
                    Point1 = new Point(segment.Control1.X, segment.Control1.Y),
                    Point2 = new Point(segment.Control2.X, segment.Control2.Y),
                    Point3 = new Point(segment.End.X, segment.End.Y),
                });
            }

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            canvas.Children.Add(new Microsoft.UI.Xaml.Shapes.Path
            {
                Data = geometry,
                Stroke = brush,
                StrokeThickness = Fluent.ChartStroke,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            });
        }

        var line = FluentType.Caption.LineHeight;
        for (var i = 0; i < points.Count; i++)
        {
            if (points[i] is not { } p)
            {
                continue;
            }

            var dot = new Ellipse { Width = CurveDot, Height = CurveDot, Fill = brush };
            Canvas.SetLeft(dot, p.X - (CurveDot / 2));
            Canvas.SetTop(dot, p.Y - (CurveDot / 2));
            canvas.Children.Add(dot);

            var text = WidgetParts.Figure(WeatherReport.Degrees(values[i]), above ? FluentType.Strong(FluentType.Caption) : FluentType.Caption,
                above ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
            text.Width = columnWidth;
            text.TextAlignment = TextAlignment.Center;
            Canvas.SetLeft(text, i * columnWidth);
            Canvas.SetTop(text, above ? p.Y - (CurveDot / 2) - line : p.Y + (CurveDot / 2));
            canvas.Children.Add(text);
        }
    }
}
