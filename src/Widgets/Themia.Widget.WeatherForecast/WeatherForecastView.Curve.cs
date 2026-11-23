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
    /// <para>
    /// [S-14810] [S-14815] Each day's column is its item: its one width from the card's left edge, the last cut by the
    /// right edge, and from the card's top edge to its foot (the hover and the links light it whole); the parts and the
    /// chart stand inside the columns, the inset over and under them their padding (WR-3).
    /// </para>
    /// <para>
    /// [S-16439] The plot is edge to edge (owner, 2026-11-19: "Plot edge to edge", the plot and its fill reach the card's
    /// edges, the labels stand inside it, over the plot): the lines are held out to the left and right edges, the wash runs
    /// down to the card's foot, the rain's figures standing over it; [S-16440] with no forecast, a flat line.
    /// </para>
    /// </summary>
    private Grid BuildCurve(IReadOnlyList<ForecastDay> days, DateOnly today, ForecastCurvePlan plan)
    {
        var shown = days.Take(plan.Days).ToList();
        var columns = new Grid
        {
            Margin = ListEdge.Margin,
            HorizontalAlignment = HorizontalAlignment.Left,
            FlowDirection = FlowDirection.LeftToRight,
        };
        AddColumns(columns, plan.ColumnWidth, shown.Count);

        // The parts over the columns, in the same columns: the pointer goes to the column under them.
        var grid = new Grid
        {
            Padding = ColumnPadding,
            RowSpacing = ForecastCurve.Gap,
            IsHitTestVisible = false,
        };
        AddColumns(grid, plan.ColumnWidth, shown.Count);
        Grid.SetColumnSpan(grid, Math.Max(1, shown.Count));

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
            var day = shown[i];

            // The day's column, the card's top edge to its foot: its summary, and its marker for the links ([S-11924]):
            // the hover fill behind the column's parts and the line.
            var hit = new Border { Background = Family.Brush(ColourRole.Clear) };
            ToolTipService.SetToolTip(hit, Describe(day));
            LinkDay(hit, day.Day);
            ItemMark.Mark(hit);
            Grid.SetColumn(hit, i);
            columns.Children.Add(hit);

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

        // [S-16439] The chart's box from its band's top to the card's foot (over the rain's line and the column's padding), the
        // first of the parts so the figures stand over its wash.
        // A card wider than its days (14 at most) runs the plot on past the last day's column to its right edge.
        var right = Math.Max(0, _laid.Width - (shown.Count * plan.ColumnWidth));
        var chart = Chart(shown, plan, shown.Count * plan.ColumnWidth + right);
        Grid.SetRow(chart, chartRow);
        Grid.SetRowSpan(chart, grid.RowDefinitions.Count - chartRow);
        Grid.SetColumnSpan(chart, Math.Max(1, shown.Count));
        chart.Margin = new Thickness(0, 0, -right, -ColumnPadding.Bottom);
        grid.Children.Insert(0, chart);
        columns.Children.Add(grid);
        return columns;
    }

    private static void Put(Grid grid, FrameworkElement element, int row, int column)
    {
        element.HorizontalAlignment = HorizontalAlignment.Center;
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }

    /// <summary>The lines and their figures, at the plan's size (known before layout, so no size handler).</summary>
    private static Grid Chart(List<ForecastDay> days, ForecastCurvePlan plan, double end)
    {
        var chart = new Grid { IsHitTestVisible = false };
        var figures = new Canvas();

        var highs = days.Select(d => d.High).ToList();
        var lows = days.Select(d => d.Low).ToList();
        var (coldest, warmest) = ForecastCurve.Scale(plan.Low ? highs.Concat(lows) : highs);

        // [S-16440] Nothing read: the default line across the plot, unavailable (WR-10), never no chart.
        if (!highs.Any(h => h is { } v && double.IsFinite(v)))
        {
            var flat = new Chart();
            flat.Role(ColourRole.TextDisabled);
            flat.Show(new ChartModel { Low = double.NaN, Series = [.. ForecastCurve.Unread(end, plan.Top, plan.Bottom).Select(p => new ChartSample(p.X, p.Y))] });
            chart.Children.Add(flat);
        }

        var highPoints = ForecastCurve.Points(highs, coldest, warmest, plan.ColumnWidth, plan.Top, plan.Bottom);
        Lines(chart, highPoints, high: true, days.Count, end);
        Figures(figures, highPoints, highs, plan.ColumnWidth, above: true);

        if (plan.Low)
        {
            var lowPoints = ForecastCurve.Points(lows, coldest, warmest, plan.ColumnWidth, plan.Top, plan.Bottom);
            Lines(chart, lowPoints, high: false, days.Count, end);
            Figures(figures, lowPoints, lows, plan.ColumnWidth, above: false);
        }

        chart.Children.Add(figures);
        return chart;
    }

    /// <summary>
    /// One line per unbroken run of days ([S-6164]): the highs as the one chart's series (the accent,
    /// its wash under it), the lows as its second series (tertiary). [S-16439] A run on the first or the last day is held out
    /// to that edge of the plot.
    /// </summary>
    private static void Lines(Grid chart, IReadOnlyList<CurvePoint?> points, bool high, int days, double end)
    {
        foreach (var run in ForecastCurve.Smooth(points))
        {
            var edged = ForecastCurve.Edged(run, ForecastCurve.Sample(run), days, end);
            if (edged.Count < 2)
            {
                continue;
            }

            // A curve laid out here, its heights as given ([S-6750]: the one chart, laid).
            var line = edged.Select(p => new ChartSample(p.X, p.Y)).ToList();
            var series = new Chart();
            series.Show(new ChartModel { Low = double.NaN, Series = high ? line : [], Second = high ? [] : line });
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
