using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>The forecast's rows and columns, on the family's blocks. [S-725] [S-726] [S-727]</summary>
internal sealed partial class WeatherForecastView
{
    /// <summary>The content's box under the header: the inset either side and at the foot.</summary>
    private static readonly Thickness ContentInset = new(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);

    // ---- Rows [S-725] ------------------------------------------------------------------------

    /// <summary>
    /// One Row block per day the plan shows: the day's sky glyph, the day (today strong), its range bar,
    /// "low high"; the sky in words and the precipitation as the second line where the tier gives one.
    /// </summary>
    private StackPanel BuildRows(IReadOnlyList<ForecastDay> days, DateOnly today, Range range, ForecastRowsPlan plan)
    {
        var list = new StackPanel { VerticalAlignment = VerticalAlignment.Top };

        for (var i = 0; i < Math.Min(plan.Days, days.Count); i++)
        {
            var day = days[i];
            var row = new RowBlock(RowKind.List, plan.Compact ? 0 : FamilyShape.Icon) { Lines = plan.Lines, Strong = day.Day == today };
            if (!plan.Compact)
            {
                row.LeadGlyph(LookGlyph(WeatherReport.Look(day.Code)));
            }

            row.Show(WeatherReport.DayLabel(day.Day, today), plan.Compact ? WeatherReport.Degrees(day.High) : Temperatures(day), Second(day));

            if (plan.Meter > 0 && day.Low is { } low && day.High is { } high)
            {
                var (start, end) = WeatherReport.RangeBar(low, high, range.Coldest, range.Warmest);
                var bar = new ChartBar();
                bar.Show(end, start);
                row.Meter(bar, plan.Meter);
            }

            ToolTipService.SetToolTip(row, Describe(day));
            LinkDay(row, day.Day);
            list.Children.Add(row);
        }

        return list;
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
    /// The days that have a column's floor, side by side in equal columns across the content: the day,
    /// the sky's glyph, the high, the range standing up, the low and the precipitation, as the plan has
    /// them. Never a scroller: a day that does not fit whole is left out ([S-726]).
    /// </summary>
    private Grid BuildColumns(IReadOnlyList<ForecastDay> days, DateOnly today, Range range, ForecastColumnsPlan plan)
    {
        var count = Math.Min(plan.Days, days.Count);
        var grid = new Grid { Margin = ContentInset, FlowDirection = FlowDirection.LeftToRight };

        for (var i = 0; i < count; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var column = BuildColumn(days[i], today, range, plan);
            Grid.SetColumn(column, i);
            grid.Children.Add(column);
        }

        return grid;
    }

    private Grid BuildColumn(ForecastDay day, DateOnly today, Range range, ForecastColumnsPlan plan)
    {
        var parts = new Grid { RowSpacing = WeatherForecastLayout.ColumnGap, Background = Family.Brush(ColourRole.Clear) };

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
