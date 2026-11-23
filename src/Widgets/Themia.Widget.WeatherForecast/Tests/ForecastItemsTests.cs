using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Items;
using Xunit;

namespace Themia.Widget.WeatherForecast.Tests;

/// <summary>[S-16974] The forecast on the item base: a day as an item in each style, its sizes and its config.</summary>
public sealed class ForecastItemsTests
{
    private static readonly DateOnly Today = new(2026, 11, 12);

    private static readonly ForecastDay Rainy = new(Today, 61, 6, 13, 2.6) { PrecipitationChance = 75 };

    private static Item Of(ForecastStyle style, bool precipitation = true, string? problem = null)
        => ForecastItems.Item(Rainy, style, Today, 3, 14, precipitation, Locale.En, problem);

    [Fact]
    public void S16974_S725_a_tables_day_is_its_sky_its_day_its_range_and_low_high()
    {
        var item = Of(ForecastStyle.Table);
        Assert.Equal(("2026-11-12", "Today", true), (item.Id, item.Title, item.Strong));
        Assert.Equal(WeatherGlyph.Of(WeatherReport.Look(61)), item.Lead!.Glyph);
        Assert.Equal($"{WeatherReport.Degrees(6)} {WeatherReport.Degrees(13)}", item.End!.Value);

        // [S-16591] The bar on the scale of every day laid: 6..13 of 3..14.
        Assert.Equal((3.0 / 11, 10.0 / 11), (item.Bar!.From, item.Bar.To), new ToleranceComparer());

        // [S-11147] The sky in words with its chance and its amount, the precipitation on; the sky alone off.
        Assert.StartsWith(WeatherReport.Describe(Locale.En, 61) + " · 75", item.Subtitle, StringComparison.Ordinal);
        Assert.Equal(WeatherReport.Describe(Locale.En, 61), Of(ForecastStyle.Table, precipitation: false).Subtitle);
    }

    [Fact]
    public void S16974_S726_a_row_lists_day_is_the_high_over_its_bar_the_low_under_it_and_the_rain_at_its_foot()
    {
        var item = Of(ForecastStyle.RowList);
        Assert.Equal((WeatherReport.Degrees(13), WeatherReport.Degrees(6)), (item.End!.Value, item.Subtitle));
        Assert.Equal(WeatherReport.PrecipitationAmount(Locale.En, 2.6), item.Note);

        // A dry day keeps the rain's line, blank, so the columns line up.
        Assert.Equal(" ", ForecastItems.Item(Rainy with { Precipitation = 0 }, ForecastStyle.RowList, Today, 3, 14, true, Locale.En).Note);
        Assert.DoesNotContain(ItemPart.Subtitle, ForecastItems.Hidden(ForecastStyle.RowList, [ItemPart.Subtitle]));
    }

    [Fact]
    public void S16974_S6171_a_grid_cell_is_the_day_the_sky_and_the_high_beside_the_low()
    {
        var item = Of(ForecastStyle.Grid);
        Assert.Equal((WeatherReport.Degrees(13), WeatherReport.Degrees(6)), (item.End!.Value, item.End.Second));
        Assert.Contains(ItemPart.Bar, ForecastItems.Hidden(ForecastStyle.Grid, []));
        Assert.Equal((80.0, 100.0), ItemLayout.Size(ItemStyle.Grid, ForecastItems.Shape(ForecastStyle.Grid, [], true), ForecastItems.Cells, ItemGrid.Default()));
    }

    [Fact]
    public void S16440_S16441_no_figures_keep_the_empty_track_and_the_problem_glyph_leads_the_first_day()
    {
        var unread = ForecastItems.Item(new ForecastDay(Today, null, null, null, null), ForecastStyle.Table, Today, 0, 1, true, Locale.En);
        Assert.Equal((0.0, 0.0), (unread.Bar!.From, unread.Bar.To));
        Assert.Equal("--", unread.End!.Value.Split(' ')[0]);

        var stale = Of(ForecastStyle.Table, problem: "No network");
        Assert.Equal((Themia.App.Shell.Glyphs.Warning, "No network"), (stale.Lead!.Glyph, stale.Tooltip));
    }

    [Fact]
    public void S16974_S11146_each_style_keeps_its_sizes()
    {
        var table = WeatherForecastLayout.Sizing(new WeatherForecastConfig());
        Assert.Equal((320.0, 5 * 30.0), (table.Natural.Width, table.Natural.Height));

        // [S-726] Columns of 60, one height: the day, the sky, the high, the standing bar, the low, the rain (184, 190 in whole cells).
        var row = WeatherForecastLayout.Sizing(new WeatherForecastConfig { Style = ForecastStyle.RowList });
        Assert.Equal((5 * 60.0, 190.0), (row.Natural.Width, row.Natural.Height));
        Assert.Equal(row.Min.Height, row.Max.Height);
        Assert.Equal(170, WeatherForecastLayout.Sizing(new WeatherForecastConfig { Style = ForecastStyle.RowList, ShowPrecipitation = false }).Natural.Height);

        // [S-6171] A new grid three cells across, two lines down.
        var grid = WeatherForecastLayout.Sizing(new WeatherForecastConfig { Style = ForecastStyle.Grid });
        Assert.Equal((240.0, 200.0), (grid.Natural.Width, grid.Natural.Height));
    }

    [Fact]
    public void S16974_a_new_forecast_is_the_table_without_its_conditions_and_a_copy_keeps_its_parts()
    {
        var config = new WeatherForecastConfig();
        Assert.Equal((ForecastStyle.Table, false), (config.Style, config.ColumnTitles));
        Assert.Equal([ItemPart.Subtitle], config.HiddenParts);

        var copy = (WeatherForecastConfig)new WeatherForecastConfig { Style = ForecastStyle.Curve, Gap = 2, HiddenParts = [], PartOrder = [ItemPart.End] }.Clone();
        Assert.Equal((ForecastStyle.Curve, 2, 0, ItemPart.End), (copy.Style, copy.Gap, copy.HiddenParts.Count, copy.PartOrder.Single()));

        var wild = new WeatherForecastConfig { Gap = 9 };
        Assert.NotEmpty(wild.Repair());
        Assert.Equal(4, wild.Gap);
    }

    private sealed class ToleranceComparer : IEqualityComparer<(double, double)>
    {
        public bool Equals((double, double) x, (double, double) y) => Math.Abs(x.Item1 - y.Item1) < 1e-9 && Math.Abs(x.Item2 - y.Item2) < 1e-9;

        public int GetHashCode((double, double) obj) => 0;
    }
}
