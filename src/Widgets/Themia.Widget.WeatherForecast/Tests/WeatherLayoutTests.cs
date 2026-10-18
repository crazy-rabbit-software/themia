using System.Globalization;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.WeatherForecast.Tests;

/// <summary>
/// [S-724]..[S-726] The forecast's disclosure ladders, at the seven size tiers (each card less its
/// ~44px header).
/// </summary>
public class WeatherLayoutTests
{
    private static readonly WidgetArea Xs = new(120, 76);
    private static readonly WidgetArea S = new(200, 106);
    private static readonly WidgetArea M = new(320, 196);
    private static readonly WidgetArea Wide = new(480, 116);
    private static readonly WidgetArea Tall = new(200, 356);
    private static readonly WidgetArea L = new(560, 376);
    private static readonly WidgetArea Xl = new(800, 516);

    // ---- forecast [S-724]..[S-726] -------------------------------------------------------

    private static readonly WidgetArea[] Tiers = [Xs, S, M, Wide, Tall, L, Xl];

    [Fact]
    public void S725_no_row_is_thinner_than_the_floor_at_any_tier_or_day_count()
    {
        // The bug: under a 200px list the rows went to 32 and scrolled, two and a half slivers on a
        // small tile. A row is never under the floor; fewer days are shown instead.
        foreach (var tier in Tiers)
        {
            for (var days = 1; days <= 14; days++)
            {
                Assert.True(WeatherForecastLayout.Rows(tier, days).RowHeight >= WeatherForecastLayout.RowMin - 1e-9, $"{tier} {days} days");
            }
        }
    }

    [Fact]
    public void S725_rows_stretch_to_fill_when_the_days_fit()
    {
        var plan = WeatherForecastLayout.Rows(Tall, 7);

        Assert.Equal(7, plan.Days);
        Assert.True(plan.RowHeight > WeatherForecastLayout.RowMin);
        Assert.True(plan.TwoLine, "a tall narrow row carries the sky on a second line");
    }

    [Fact]
    public void S725_more_days_than_fit_show_the_first_ones_at_the_floor_or_more()
    {
        // M's list is 182 tall: four rows of 45, not fourteen rows of 32 in a scroller.
        var plan = WeatherForecastLayout.Rows(M, 14);

        Assert.Equal(4, plan.Days);
        Assert.Equal((M.Height - WeatherForecastLayout.ListPadding) / 4, plan.RowHeight, 6);
    }

    [Fact]
    public void S725_a_wide_list_gains_the_sky_the_precipitation_and_its_chance()
    {
        var plan = WeatherForecastLayout.Rows(L, 7);

        Assert.True(plan.Description);
        Assert.True(plan.Bar);
        Assert.True(plan.Precipitation);
        Assert.True(plan.Chance);
        Assert.False(plan.TwoLine);
    }

    [Fact]
    public void S725_a_narrow_list_keeps_the_day_and_its_high()
    {
        var plan = WeatherForecastLayout.Rows(new WidgetArea(120, 300), 7);

        Assert.False(plan.IconAndLow);
        Assert.False(plan.Bar);
        Assert.False(plan.Description);
    }

    [Fact]
    public void S725_the_largest_tile_gives_the_rows_the_whole_width_and_no_today_panel()
    {
        // The rows are the forecast; a Today panel beside them repeated the first row (retired).
        var xl = WeatherForecastLayout.Rows(Xl, 7);
        Assert.True(xl.Description);
        Assert.True(xl.Chance);
        Assert.True(xl.Wind);
        Assert.True(WeatherForecastLayout.Rows(L, 7).Wind);
    }

    [Fact]
    public void S726_a_column_never_draws_more_than_its_height_holds()
    {
        // The smallest tile: day and high only. A glyph here clipped the low off the card.
        var xs = WeatherForecastLayout.Columns(Xs, 7);
        Assert.False(xs.Icon);
        Assert.False(xs.Low);

        var wide = WeatherForecastLayout.Columns(Wide, 7);
        Assert.True(wide.Icon);
        Assert.True(wide.Low);
        Assert.Equal(7, wide.Days);
    }

    [Fact]
    public void S726_columns_show_fewer_days_rather_than_squeeze_below_their_minimum()
    {
        // S is 184 inside its padding: three whole columns, not three and a sliver of a fourth.
        var plan = WeatherForecastLayout.Columns(S, 14);

        Assert.Equal(3, plan.Days);
        Assert.True(plan.ColumnWidth >= WeatherForecastLayout.ColumnMin);
    }

    [Fact]
    public void S726_a_tall_column_gains_the_sky_chance_and_wind()
    {
        var plan = WeatherForecastLayout.Columns(Xl, 7);

        Assert.True(plan.Bar);
        Assert.True(plan.Description);
        Assert.True(plan.Chance);
        Assert.True(plan.Wind);
    }

    [Fact]
    public void S6163_the_style_survives_a_clone()
        => Assert.Equal(ForecastStyle.Curve, ((WeatherForecastConfig)new WeatherForecastConfig { Style = ForecastStyle.Curve }.Clone()).Style);
}
