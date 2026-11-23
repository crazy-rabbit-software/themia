using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.Items;
using Themia.Sdk.Kit;
using Themia.Widget.WeatherForecast;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-16974] The forecast's days on the item base (owner, 2026-11-23: "the weather should be integrated into the same component
/// too, the current weather forecast row view will be the new table view for this widget ... weather should not loose any
/// features"): a day as an item in each arrangement, the arrangement a style is, the parts a day has and their words, and
/// the days' own measures (the row list's 60 columns, the grid's 80 × 100 cells). Pure.
/// </summary>
public static class ForecastItems
{
    /// <summary>[S-16974] The parts a day offers on its page: its sky, its day, the sky in words, the range bar, its figures.</summary>
    public static IReadOnlyList<ItemPart> Parts { get; } = [ItemPart.Lead, ItemPart.Title, ItemPart.Subtitle, ItemPart.Bar, ItemPart.End];

    /// <summary>[S-726] The narrowest column that holds "Today" and "-18°" in Body: a row list's column (60 on the default grid).</summary>
    public const double ColumnMin = 56;

    /// <summary>[S-6171] The narrowest cell content: "Today" and "16° 8°" in Body.</summary>
    public const double CellMinWidth = 56;

    /// <summary>[S-14815] A grid cell's natural width: its content and the inset either side, inside the cell (WR-3).</summary>
    public const double CellNaturalWidth = CellMinWidth + (2 * FamilySpace.Inset);

    /// <summary>[S-14815] A grid cell's natural height: the day, the 32 glyph and the figures, 4 apart, 8 over and under (100 on the default grid).</summary>
    public static double CellNaturalHeight => (2 * FamilyType.Body.LineHeight) + FamilyShape.IconLarge + (2 * FamilySpace.S1) + (2 * FamilySpace.S2);

    /// <summary>[S-16974] The days' own measures: the row list's columns and the grid's cells.</summary>
    public static ItemCells Cells { get; } = new(ColumnMin, CellNaturalWidth, CellNaturalHeight);

    /// <summary>[S-16974] The item base's arrangement of a style (the curve has none: it is its own chart).</summary>
    public static ItemStyle? Arrangement(ForecastStyle style) => style switch
    {
        ForecastStyle.Table => ItemStyle.Table,
        ForecastStyle.RowList => ItemStyle.RowList,
        ForecastStyle.Grid => ItemStyle.Grid,
        ForecastStyle.List => ItemStyle.List,
        ForecastStyle.ColumnList => ItemStyle.ColumnList,
        _ => null,
    };

    /// <summary>
    /// [S-16974] The parts left out in <paramref name="style"/>: the user's; a row list's subtitle is its low and always drawn
    /// (the column's foot under the standing bar, [S-726]); a grid's cell is the day, its sky and its figures alone ([S-6171]).
    /// </summary>
    public static IReadOnlyCollection<ItemPart> Hidden(ForecastStyle style, IReadOnlyCollection<ItemPart> hidden)
    {
        ArgumentNullException.ThrowIfNull(hidden);
        return style switch
        {
            ForecastStyle.RowList => [.. hidden.Where(p => p != ItemPart.Subtitle)],
            ForecastStyle.Grid => [.. hidden.Union([ItemPart.Subtitle, ItemPart.Bar])],
            _ => hidden,
        };
    }

    /// <summary>[S-16974] The parts a day draws in <paramref name="style"/> (its rain the row list's note where the precipitation is on).</summary>
    public static ItemShape Shape(ForecastStyle style, IReadOnlyCollection<ItemPart> hidden, bool precipitation)
        => ItemShape.Of(style == ForecastStyle.RowList && precipitation ? [.. Parts, ItemPart.Note] : Parts, Hidden(style, hidden));

    /// <summary>
    /// [S-16974] A day as an item in <paramref name="style"/>: its sky's glyph the lead (the problem glyph in the first day's place
    /// where <paramref name="problem"/>, [S-16441]), the day its title (today strong, [S-727]), its range bar on the scale of
    /// every day laid (<paramref name="coldest"/>..<paramref name="warmest"/>, its empty track without figures, [S-16440]). In the
    /// table and the lists: the sky in words with its chance and amount under it where the precipitation is on ([S-11147]),
    /// "low high" at its end in tabular figures ([S-16590]). In the row list: the high at its end over the standing bar, the low
    /// under it, the rain's amount at the foot (a blank one on a dry day, so the columns line up, [S-726]). In the grid: the high
    /// beside the low ([S-6171]).
    /// </summary>
    public static Item Item(ForecastDay day, ForecastStyle style, DateOnly today, double coldest, double warmest, bool precipitation, Locale locale, string? problem = null)
    {
        ArgumentNullException.ThrowIfNull(day);
        var lead = problem is { Length: > 0 }
            ? new ItemLead(Glyphs.Warning, ColourRole.Caution)
            : new ItemLead(WeatherGlyph.Of(WeatherReport.Look(day.Code)), ColourRole.TextSecondary);
        var bar = ItemBar.Track;
        if (day.Low is { } low && day.High is { } high)
        {
            var (from, to) = WeatherReport.RangeBar(low, high, coldest, warmest);
            bar = new ItemBar(to, from);
        }

        var label = WeatherReport.DayLabel(locale, day.Day, today);
        var tooltip = problem is { Length: > 0 } ? problem : Describe(day, locale);
        var (subtitle, end, note) = style switch
        {
            ForecastStyle.RowList => (WeatherReport.Degrees(day.Low), ItemEnd.Text(WeatherReport.Degrees(day.High), ColourRole.TextPrimary),
                precipitation ? Or(WeatherReport.PrecipitationAmount(locale, day.Precipitation)) : string.Empty),
            ForecastStyle.Grid => (string.Empty, ItemEnd.Text(WeatherReport.Degrees(day.High), ColourRole.TextPrimary, WeatherReport.Degrees(day.Low)), string.Empty),
            _ => (Second(day, precipitation, locale), ItemEnd.Text($"{WeatherReport.Degrees(day.Low)} {WeatherReport.Degrees(day.High)}", ColourRole.TextPrimary), string.Empty),
        };
        return new Item(day.Day.ToString("O", System.Globalization.CultureInfo.InvariantCulture), label, lead, subtitle, end, bar, note, tooltip, Strong: day.Day == today);
    }

    /// <summary>A blank line kept where there is nothing to say, so the columns' parts line up.</summary>
    private static string Or(string text) => text.Length > 0 ? text : " ";

    /// <summary>
    /// [S-11147] A day's second line: the sky in words, then (with the precipitation on) its chance and its amount where there is
    /// any ("Light rain · 80% · 4.2 mm").
    /// </summary>
    public static string Second(ForecastDay day, bool precipitation, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(day);
        var parts = new List<string> { WeatherReport.Describe(locale, day.Code) };
        if (precipitation)
        {
            if (day.PrecipitationChance is > 0)
            {
                parts.Add(WeatherReport.Chance(locale, day.PrecipitationChance));
            }

            var rain = WeatherReport.Precipitation(locale, day.Precipitation);
            if (rain.Length > 0)
            {
                parts.Add(rain);
            }
        }

        return string.Join(" · ", parts);
    }

    /// <summary>The day, its sky and its rain in words: a day's tooltip.</summary>
    public static string Describe(ForecastDay day, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(day);
        var parts = new List<string> { Formatting.ShortDate(day.Day), WeatherReport.Describe(locale, day.Code) };
        var rain = WeatherReport.Precipitation(locale, day.Precipitation);
        if (rain.Length > 0)
        {
            parts.Add(rain);
        }

        return string.Join(" · ", parts);
    }

    /// <summary>The coldest low and warmest high across <paramref name="days"/>, which every bar scales against ([S-16591]).</summary>
    public static (double Coldest, double Warmest) Extremes(IEnumerable<ForecastDay> days)
    {
        var coldest = double.MaxValue;
        var warmest = double.MinValue;
        foreach (var day in days ?? [])
        {
            foreach (var value in new[] { day.Low, day.High })
            {
                if (value is { } v && double.IsFinite(v))
                {
                    coldest = Math.Min(coldest, v);
                    warmest = Math.Max(warmest, v);
                }
            }
        }

        // Every day missing both numbers: the bar spans the row, which RangeBar does for no span.
        return coldest <= warmest ? (coldest, warmest) : (0, 1);
    }

    /// <summary>[S-16974] A day's words for its parts: Sky, Day, Conditions, Range, Low and high.</summary>
    public static string PartName(ItemPart part, Locale locale) => part switch
    {
        ItemPart.Lead => WeatherForecastStr.Forecast_Part_Lead.Text(locale),
        ItemPart.Title => WeatherForecastStr.Forecast_Part_Title.Text(locale),
        ItemPart.Subtitle => WeatherForecastStr.Forecast_Part_Subtitle.Text(locale),
        ItemPart.Bar => WeatherForecastStr.Forecast_Part_Bar.Text(locale),
        _ => WeatherForecastStr.Forecast_Part_End.Text(locale),
    };
}
