using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.WeatherForecast.Tests;

/// <summary>
/// [S-724]..[S-726] The forecast's rows and columns on the family's blocks, at the seven size tiers
/// (each card less its 36 header).
/// </summary>
public class WeatherLayoutTests
{
    private static readonly WidgetArea Xs = new(120, 84);
    private static readonly WidgetArea S = new(200, 114);
    private static readonly WidgetArea M = new(320, 204);
    private static readonly WidgetArea Wide = new(480, 124);
    private static readonly WidgetArea Tall = new(200, 364);
    private static readonly WidgetArea L = new(560, 384);
    private static readonly WidgetArea Xl = new(800, 524);

    private static readonly WidgetArea[] Tiers = [Xs, S, M, Wide, Tall, L, Xl];

    // ---- rows [S-725] -------------------------------------------------------------------------

    [Fact]
    public void S725_rows_are_whole_rows_of_the_tiers_lines_never_stretched_and_never_more_than_the_forecast()
    {
        foreach (var tier in Tiers)
        {
            for (var days = 1; days <= 14; days++)
            {
                var plan = WeatherForecastLayout.Rows(tier, days);
                Assert.InRange(plan.Days, 0, days);
                Assert.Equal(RowRules.Lines(tier, 2), plan.Lines);
                Assert.True(plan.Days * FamilyShape.RowHeight(plan.Lines) <= WeatherForecastLayout.ContentHeight(tier) + 0.01, $"{tier} {days}");
            }
        }

        Assert.Equal(7, WeatherForecastLayout.Rows(Tall, 7).Days);
        Assert.Equal(4, WeatherForecastLayout.Rows(M, 7).Days);
        Assert.Equal(4, WeatherForecastLayout.Rows(Wide, 7).Days);
        Assert.Equal(2, WeatherForecastLayout.Rows(Xs, 7).Days);
    }

    [Fact]
    public void S725_a_row_has_two_lines_from_a_regular_area_both_ways()
    {
        Assert.Equal(1, WeatherForecastLayout.Rows(Xs, 7).Lines);
        Assert.Equal(1, WeatherForecastLayout.Rows(Wide, 7).Lines);
        Assert.Equal(2, WeatherForecastLayout.Rows(M, 7).Lines);
        Assert.Equal(2, WeatherForecastLayout.Rows(Xl, 7).Lines);
    }

    [Fact]
    public void S725_the_range_bar_comes_with_the_width_and_stops_growing()
    {
        Assert.Equal(0, WeatherForecastLayout.Rows(S, 7).Meter);
        Assert.Equal(Math.Floor((320 - (2 * FamilySpace.Inset)) * WeatherForecastLayout.MeterShare), WeatherForecastLayout.Rows(M, 7).Meter);
        Assert.Equal(WeatherForecastLayout.MeterMax, WeatherForecastLayout.Rows(Xl, 7).Meter);
    }

    [Fact]
    public void S725_the_smallest_tile_is_the_day_and_its_high_alone()
    {
        Assert.True(WeatherForecastLayout.Rows(Xs, 7).Compact);
        Assert.All(Tiers.Skip(1), t => Assert.False(WeatherForecastLayout.Rows(t, 7).Compact));
    }

    // ---- columns [S-726] ----------------------------------------------------------------------

    [Fact]
    public void S726_columns_share_the_width_never_under_their_floor_and_never_more_than_the_forecast()
    {
        foreach (var tier in Tiers)
        {
            var plan = WeatherForecastLayout.Columns(tier, 7);
            Assert.InRange(plan.Days, 1, 7);
            Assert.Equal(WeatherForecastLayout.ContentWidth(tier), plan.Days * plan.ColumnWidth, 6);
            Assert.True(plan.ColumnWidth >= WeatherForecastLayout.ColumnMin - 1e-9 || plan.Days == 1);
        }

        Assert.Equal(1, WeatherForecastLayout.Columns(Xs, 7).Days);
        Assert.Equal(5, WeatherForecastLayout.Columns(M, 7).Days);
        Assert.Equal(7, WeatherForecastLayout.Columns(Xl, 7).Days);
        Assert.Equal(3, WeatherForecastLayout.Columns(Xl, 3).Days);
    }

    [Fact]
    public void S726_a_column_never_draws_more_than_its_height_holds()
    {
        foreach (var tier in Tiers)
        {
            foreach (var rain in new[] { true, false })
            {
                var plan = WeatherForecastLayout.Columns(tier, 7, rain);
                var parts = new List<double> { 20, 20 };
                if (plan.Icon > 0)
                {
                    parts.Add(plan.Icon);
                }

                if (plan.Low)
                {
                    parts.Add(20);
                }

                if (plan.Bar)
                {
                    parts.Add(WeatherForecastLayout.ColumnBarMin);
                }

                if (plan.Precipitation)
                {
                    parts.Add(16);
                }

                var used = parts.Sum() + ((parts.Count - 1) * WeatherForecastLayout.ColumnGap);
                Assert.True(used <= WeatherForecastLayout.ContentHeight(tier) + 0.01, $"{tier}");
                Assert.False(plan.Precipitation && !rain);
            }
        }
    }

    [Fact]
    public void S726_the_glyph_the_low_the_bar_and_the_rain_come_with_the_height()
    {
        // XS's 72 holds the day, the high and the 16 glyph, 4 apart (64); 16 shorter it does not.
        Assert.Equal(FamilyShape.Icon, WeatherForecastLayout.Columns(Xs, 7).Icon);
        var tiny = WeatherForecastLayout.Columns(new WidgetArea(120, 68), 7);
        Assert.Equal(0, tiny.Icon);
        Assert.False(tiny.Low || tiny.Bar || tiny.Precipitation);

        var strip = WeatherForecastLayout.Columns(Wide, 7);
        Assert.Equal(FamilyShape.Icon, strip.Icon);
        Assert.True(strip.Low);
        Assert.False(strip.Bar);

        var large = WeatherForecastLayout.Columns(Xl, 7);
        Assert.Equal(FamilyShape.IconLarge, large.Icon);
        Assert.True(large.Low && large.Bar && large.Precipitation);
    }

    [Fact]
    public void S6163_the_style_survives_a_clone()
        => Assert.Equal(ForecastStyle.Curve, ((WeatherForecastConfig)new WeatherForecastConfig { Style = ForecastStyle.Curve }.Clone()).Style);
}
