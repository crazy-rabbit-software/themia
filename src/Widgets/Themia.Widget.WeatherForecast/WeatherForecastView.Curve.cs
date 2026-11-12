using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>[S-6164] The forecast's temperature-curve style, on the Chart block.</summary>
internal sealed partial class WeatherForecastView
{
    /// <summary>
    /// The days as columns (the day, its glyph, its precipitation) over one chart that spans them: the
    /// highs as the Chart block's line in the accent with each day's figure over its point, and with
    /// the height the lows as its second line, tertiary, their figures under them, on the same scale.
    /// Time runs left to right in every language ([S-1113]).
    /// </summary>
    private Grid BuildCurve(IReadOnlyList<ForecastDay> days, DateOnly today, ForecastCurvePlan plan)
    {
        var shown = days.Take(plan.Days).ToList();
        var grid = new Grid
        {
            Margin = ContentInset,
            RowSpacing = ForecastCurve.Gap,
            FlowDirection = FlowDirection.LeftToRight,
        };

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        if (plan.Icon)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        var chartRow = grid.RowDefinitions.Count;
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(plan.ChartHeight) });
        if (plan.Precipitation)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(FamilyType.Caption.LineHeight) });
        }

        for (var i = 0; i < shown.Count; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var day = shown[i];

            // A hit target per column for the day's summary; the chart itself is not hit-testable. It is the
            // day's column marker for the links ([S-11924]): the hover fill behind the column's parts and the line.
            var hit = new Border { Background = Family.Brush(ColourRole.Clear), CornerRadius = Family.Radius };
            ToolTipService.SetToolTip(hit, Describe(day));
            LinkDay(hit, day.Day);
            Grid.SetColumn(hit, i);
            Grid.SetRowSpan(hit, grid.RowDefinitions.Count);
            grid.Children.Add(hit);

            Put(grid, DayText(day, today), 0, i);

            if (plan.Icon)
            {
                Put(grid, Family.Glyph(LookGlyph(WeatherReport.Look(day.Code))), 1, i);
            }

            if (plan.Precipitation)
            {
                Put(grid, Family.Text(FamilyType.Caption, ColourRole.TextSecondary, WeatherReport.PrecipitationAmount(day.Precipitation)), chartRow + 1, i);
            }
        }

        var chart = Chart(shown, plan);
        Grid.SetRow(chart, chartRow);
        Grid.SetColumnSpan(chart, Math.Max(1, shown.Count));
        grid.Children.Add(chart);
        return grid;
    }

    private static void Put(Grid grid, FrameworkElement element, int row, int column)
    {
        element.HorizontalAlignment = HorizontalAlignment.Center;
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }

    /// <summary>The lines and their figures, at the plan's size (known before layout, so no size handler).</summary>
    private static Grid Chart(List<ForecastDay> days, ForecastCurvePlan plan)
    {
        var chart = new Grid { IsHitTestVisible = false, Height = plan.ChartHeight, VerticalAlignment = VerticalAlignment.Top };
        var figures = new Canvas();

        var highs = days.Select(d => d.High).ToList();
        var lows = days.Select(d => d.Low).ToList();
        var (coldest, warmest) = ForecastCurve.Scale(plan.Low ? highs.Concat(lows) : highs);

        var highPoints = ForecastCurve.Points(highs, coldest, warmest, plan.ColumnWidth, plan.Top, plan.Bottom);
        Lines(chart, highPoints, high: true);
        Figures(figures, highPoints, highs, plan.ColumnWidth, above: true);

        if (plan.Low)
        {
            var lowPoints = ForecastCurve.Points(lows, coldest, warmest, plan.ColumnWidth, plan.Top, plan.Bottom);
            Lines(chart, lowPoints, high: false);
            Figures(figures, lowPoints, lows, plan.ColumnWidth, above: false);
        }

        chart.Children.Add(figures);
        return chart;
    }

    /// <summary>
    /// One line per unbroken run of days ([S-6164]): the highs as the Chart block's series (the accent,
    /// its wash under it), the lows as its second series (tertiary).
    /// </summary>
    private static void Lines(Grid chart, IReadOnlyList<CurvePoint?> points, bool high)
    {
        foreach (var run in ForecastCurve.Smooth(points))
        {
            if (run.Segments.Count == 0)
            {
                continue;
            }

            var line = ForecastCurve.Sample(run).Select(p => new ChartPoint(p.X, p.Y)).ToList();
            var series = new ChartSeries();
            series.Show(high ? line : [], high ? null : line);
            chart.Children.Add(series);
        }
    }

    /// <summary>Each day's figure in Caption over its point (the highs, primary) or under it (the lows, secondary), on its column.</summary>
    private static void Figures(Canvas canvas, IReadOnlyList<CurvePoint?> points, List<double?> values, double columnWidth, bool above)
    {
        var line = FamilyType.Caption.LineHeight;
        var edge = FamilyShape.Stroke / 2;
        for (var i = 0; i < points.Count; i++)
        {
            if (points[i] is not { } p)
            {
                continue;
            }

            // The figure centred on its column: a column-wide box, the text in its middle.
            var text = Family.Text(FamilyType.Caption, above ? ColourRole.TextPrimary : ColourRole.TextSecondary, WeatherReport.Degrees(values[i]));
            text.HorizontalAlignment = HorizontalAlignment.Center;
            var slot = new Grid { Width = columnWidth, Children = { text } };
            Canvas.SetLeft(slot, i * columnWidth);
            Canvas.SetTop(slot, above ? p.Y - edge - line : p.Y + edge);
            canvas.Children.Add(slot);
        }
    }
}
