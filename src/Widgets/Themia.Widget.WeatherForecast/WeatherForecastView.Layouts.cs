using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>The forecast's rows and columns, on the family's blocks. [S-725] [S-726] [S-727]</summary>
internal sealed partial class WeatherForecastView
{
    /// <summary>The content's box: the inset either side and at the foot, joined or alone ([S-12483]).</summary>
    private Thickness ContentInset => new(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);

    /// <summary>
    /// [S-14810] [S-14815] A column of a day's parts: from the card's top edge to its foot (the columns stand on
    /// <see cref="ListEdge"/>, the inset over and under the parts their padding inside the column, WR-3), from the card's
    /// left edge.
    /// </summary>
    private static Thickness ColumnPadding => new(0, FamilySpace.Inset, 0, FamilySpace.Inset);

    /// <summary>[S-14815] The columns' widths, each its one size on whole device pixels, from the card's left edge.</summary>
    private void AddColumns(Grid grid, double width, int count)
    {
        var reach = new RowReach(width, count, _laid.Grid().Raster);
        for (var i = 0; i < count; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(reach.Height(i)) });
        }
    }

    // ---- Rows [S-725] ------------------------------------------------------------------------

    /// <summary>
    /// One Row block per day the plan shows: the day's sky glyph, the day (today strong), its range bar,
    /// "low high"; the sky in words and the precipitation as the second line where the tier gives one.
    /// <para>
    /// [S-12807] The rows reach the content's top and the foot's inset (<see cref="SpreadStack"/>, the foot's
    /// inset its margin): the first day's text on the card's inset, the last day's on the foot's; across, each bar takes what
    /// the widest day and the widest value leave, the same length on every row, starting and ending at the same x ([S-16590]).
    /// </para>
    /// </summary>
    private SpreadStack BuildRows(IReadOnlyList<ForecastDay> days, DateOnly today, Range range, ForecastRowsPlan plan)
    {
        var list = new RowStack { Margin = ListEdge.Margin };
        var bars = new List<(RowBlock Row, ChartBar Bar)>();

        for (var i = 0; i < Math.Min(plan.Days, days.Count); i++)
        {
            var day = days[i];
            var row = new RowBlock(RowKind.List, plan.Compact ? 0 : FamilyShape.Icon) { Lines = plan.Lines, Strong = day.Day == today };
            if (!plan.Compact && i == 0 && _shownProblems.Count > 0)
            {
                // [S-16441] The problem glyph in the first day's lead, in place of its sky: no row gives up room for it.
                row.Lead(ToolLine.Problem(string.Join("\n", _shownProblems)));
            }
            else if (!plan.Compact)
            {
                row.LeadGlyph(LookGlyph(WeatherReport.Look(day.Code)));
            }

            row.Show(WeatherReport.DayLabel(day.Day, today), plan.Compact ? WeatherReport.Degrees(day.High) : Temperatures(day), Second(day));

            // [S-16440] A day without its figures keeps its bar's track (WR-10: the default values, never no bar).
            if (plan.Meter > 0)
            {
                var bar = new ChartBar();
                if (day.Low is { } low && day.High is { } high)
                {
                    var (start, end) = WeatherReport.RangeBar(low, high, range.Coldest, range.Warmest);
                    bar.Show(end, start);
                }

                bars.Add((row, bar));
            }

            ToolTipService.SetToolTip(row, Describe(day));
            LinkDay(row, day.Day);
            list.Children.Add(row);
        }

        if (bars.Count > 0)
        {
            // [S-16590] Every row's bar starts and ends at the same x: the values in tabular figures, their column the widest
            // value's on every row (the day's column takes what is left, the lead its one width, a problem glyph's too).
            var rows = list.Children.OfType<RowBlock>().ToList();
            foreach (var row in rows)
            {
                Family.Figures(row.ValueText);
            }

            var values = WeatherForecastLayout.ValueColumn(rows.Select(r => r.ValueText.Visibility == Visibility.Visible ? Natural(r.ValueText) : 0));
            var meter = WeatherForecastLayout.MeterWidth(
                WeatherForecastLayout.ContentWidth(_laid),
                plan.Compact ? 0 : FamilyShape.Icon + FamilySpace.GlyphGap,
                rows.Max(r => Math.Max(Natural(r.NameText), r.Second.Visibility == Visibility.Visible ? Natural(r.Second) : 0)),
                values,
                plan.Meter);
            foreach (var (row, bar) in bars)
            {
                row.ValueColumn(values);
                row.Meter(bar, meter);
            }
        }

        return list;
    }

    /// <summary>A text's own width on one line, as it is styled now.</summary>
    private static double Natural(TextBlock text)
    {
        text.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        return text.DesiredSize.Width;
    }

    /// <summary>A row's value: the low, then the high, as the range bar runs cold to warm.</summary>
    private static string Temperatures(ForecastDay day) => $"{WeatherReport.Degrees(day.Low)} {WeatherReport.Degrees(day.High)}";

    /// <summary>
    /// A row's second line: the sky in words, then (with the precipitation on) its chance and its
    /// amount where there is any ("Light rain · 80% · 4.2 mm").
    /// </summary>
    private string Second(ForecastDay day)
    {
        var parts = new List<string> { WeatherReport.Describe(day.Code) };
        if (_config.ShowPrecipitation)
        {
            if (day.PrecipitationChance is > 0)
            {
                parts.Add(WeatherReport.Chance(day.PrecipitationChance));
            }

            var rain = WeatherReport.Precipitation(day.Precipitation);
            if (rain.Length > 0)
            {
                parts.Add(rain);
            }
        }

        return string.Join(" · ", parts);
    }

    // ---- Columns [S-726] ---------------------------------------------------------------------

    /// <summary>
    /// The days side by side, each a column of its one fixed width from the card's left edge and from its top edge to its
    /// foot ([S-14810], [S-14815]): the day, the
    /// sky's glyph, the high, the range standing up, the low and the precipitation, as the plan has them. Never a
    /// scroller: the last day the card's right edge reaches is cut by it.
    /// </summary>
    private Grid BuildColumns(IReadOnlyList<ForecastDay> days, DateOnly today, Range range, ForecastColumnsPlan plan)
    {
        var count = Math.Min(plan.Days, days.Count);
        var grid = new Grid { Margin = ListEdge.Margin, FlowDirection = FlowDirection.LeftToRight, HorizontalAlignment = HorizontalAlignment.Left };
        AddColumns(grid, plan.ColumnWidth, count);

        for (var i = 0; i < count; i++)
        {
            var column = BuildColumn(days[i], today, range, plan);
            Grid.SetColumn(column, i);
            grid.Children.Add(column);
        }

        return grid;
    }

    private Grid BuildColumn(ForecastDay day, DateOnly today, Range range, ForecastColumnsPlan plan)
    {
        var parts = new Grid { RowSpacing = WeatherForecastLayout.ColumnGap, Padding = ColumnPadding, Background = Family.Brush(ColourRole.Clear) };

        void Add(FrameworkElement element, GridLength height)
        {
            parts.RowDefinitions.Add(new RowDefinition { Height = height });
            Grid.SetRow(element, parts.RowDefinitions.Count - 1);
            element.HorizontalAlignment = HorizontalAlignment.Center;
            parts.Children.Add(element);
        }

        Add(DayText(day, today), GridLength.Auto);

        if (plan.Icon > 0)
        {
            Add(Family.Glyph(LookGlyph(WeatherReport.Look(day.Code)), plan.Icon), GridLength.Auto);
        }

        Add(Family.Text(FamilyType.Body, ColourRole.TextPrimary, WeatherReport.Degrees(day.High)), GridLength.Auto);

        if (plan.Bar)
        {
            var bar = new ChartBar(vertical: true);
            if (day.Low is { } low && day.High is { } high)
            {
                var (start, end) = WeatherReport.RangeBar(low, high, range.Coldest, range.Warmest);
                bar.Show(end, start);
            }

            Add(bar, new GridLength(1, GridUnitType.Star));
        }

        if (plan.Low)
        {
            Add(Family.Text(FamilyType.Body, ColourRole.TextSecondary, WeatherReport.Degrees(day.Low)), GridLength.Auto);
        }

        // A fixed line, rain or not, so the columns' parts line up across a dry day and a wet one.
        if (plan.Precipitation)
        {
            Add(Family.Text(FamilyType.Caption, ColourRole.TextSecondary, WeatherReport.PrecipitationAmount(day.Precipitation)), new GridLength(FamilyType.Caption.LineHeight));
        }

        // Without a bar the parts keep together at the top; the room left stays under them.
        if (!plan.Bar)
        {
            parts.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        }

        ToolTipService.SetToolTip(parts, Describe(day));
        LinkDay(parts, day.Day);
        ItemMark.Mark(parts);
        return parts;
    }

    // ---- parts -------------------------------------------------------------------------------

    /// <summary>[S-727] "Today" or the weekday, in Body; today strong, never in the accent.</summary>
    private static TextBlock DayText(ForecastDay day, DateOnly today)
        => Family.Text(day.Day == today ? FamilyType.BodyStrong : FamilyType.Body, ColourRole.TextPrimary, WeatherReport.DayLabel(day.Day, today));

    private static string Describe(ForecastDay day)
    {
        var parts = new List<string> { Formatting.ShortDate(day.Day), WeatherReport.Describe(day.Code) };
        var rain = WeatherReport.Precipitation(day.Precipitation);

        if (rain.Length > 0)
        {
            parts.Add(rain);
        }

        return string.Join(" · ", parts);
    }
}
