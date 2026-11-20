using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.WeatherForecast.Tests;

/// <summary>[S-724]..[S-726] [S-11145] [S-11146] The forecast as an atom: its days decide what it shows, each style's size follows.</summary>
public class WeatherLayoutTests
{
    private static readonly WidgetDescriptor Kind = new WeatherForecastModule().Descriptor;

    [Fact]
    public void S11145_a_new_forecast_shows_five_days_on_one_line_rows()
    {
        var config = new WeatherForecastConfig();
        Assert.Equal((5, 1, ForecastStyle.Rows), (WeatherForecastLayout.DaysIn(default, config), config.Lines, config.Style));
        Assert.Equal(new SizeDip(300, 5 * 30), Kind.Sizing(config).Natural);
    }

    [Fact]
    public void S11146_S14500_rows_are_a_row_a_day_every_cell_from_one_day_up_220_to_420_wide()
    {
        var one = Kind.Sizing(new WeatherForecastConfig());
        var two = Kind.Sizing(new WeatherForecastConfig { Lines = 2 });

        Assert.Equal(new SizeDip(300, 5 * 30), one.Natural);
        Assert.Equal(5 * 40, two.Natural.Height);
        Assert.Equal((220.0, Themia.Sdk.SizeStretch.Ceiling), (one.Min.Width, one.Max.Width)); // relaxed, [S-12411]: no practical Max
        Assert.Equal(30, one.Min.Height);
        Assert.Equal(137, one.FitHeight(137));
    }

    [Fact]
    public void S11146_columns_are_a_56_column_a_day_every_cell_wide_at_one_height_new()
    {
        var sizing = Kind.Sizing(new WeatherForecastConfig { Style = ForecastStyle.Columns });
        var dry = Kind.Sizing(new WeatherForecastConfig { Style = ForecastStyle.Columns, ShowPrecipitation = false });

        Assert.Equal(24 + (5 * 56), sizing.Natural.Width);
        Assert.Equal(sizing.Min.Height, sizing.Natural.Height);
        Assert.Equal(24 + 56, sizing.Min.Width);
        Assert.Equal(SizeStretch.Ceiling, sizing.Max.Height); // relaxed, [S-12411]
        Assert.Equal(12 + WeatherForecastLayout.ColumnHeight(true) + 12, sizing.Natural.Height);
        Assert.Equal(20, sizing.Natural.Height - dry.Natural.Height);
    }

    [Fact]
    public void S11146_S14502_the_days_are_as_many_as_the_box_holds_never_stored()
    {
        // Owner, 2026-11-20: "Anything that's just 'how many of the same item' (days, hours, weeks, processes) follows the
        // card's size": a rows card ten cells and a bit tall (its view 98 under the inset) shows three days, eleven cells
        // three too, twelve four; a columns card three columns wide three.
        var rows = new WeatherForecastConfig();
        var columns = new WeatherForecastConfig { Style = ForecastStyle.Columns };

        Assert.Equal(3, WeatherForecastLayout.DaysIn(new WidgetArea(300, 100 - 12), rows));
        Assert.Equal(3, WeatherForecastLayout.DaysIn(new WidgetArea(300, 110 - 12), rows));
        Assert.Equal(4, WeatherForecastLayout.DaysIn(new WidgetArea(300, 120 - 12), rows));
        Assert.Equal(14, WeatherForecastLayout.DaysIn(new WidgetArea(300, 2000), rows));
        Assert.Equal(3, WeatherForecastLayout.DaysIn(new WidgetArea(24 + (3 * 56) + 20, 200), columns));
        Assert.Equal(14, rows.ForecastDays);
    }

    [Fact]
    public void S725_rows_show_the_configs_days_and_lines_and_the_bar_is_a_share_of_the_row()
    {
        var plan = WeatherForecastLayout.Rows(new WidgetArea(320, 200), new WeatherForecastConfig { Lines = 2 });

        // [S-14502] 212 from the card's top edge holds five rows of two lines (40).
        Assert.Equal((5, 2, false), (plan.Days, plan.Lines, plan.Compact));
        Assert.Equal(Math.Floor((320 - 24) * WeatherForecastLayout.MeterShare), plan.Meter);
        Assert.Equal(WeatherForecastLayout.MeterMax, WeatherForecastLayout.Rows(new WidgetArea(800, 200), new WeatherForecastConfig()).Meter);
    }

    [Fact]
    public void S12403_the_bar_takes_what_the_widest_name_and_value_leave()
    {
        // 396 inside, a 16 lead and its 8, "Today" 38.4, "-12° 16°" 51.2, 12 either side of the bar.
        Assert.Equal(396 - 24 - 39 - 52 - 24, WeatherForecastLayout.MeterWidth(396, 24, 38.4, 51.2, 126));
    }

    [Fact]
    public void S12403_a_long_second_line_keeps_the_bar_at_its_share()
        => Assert.Equal(88, WeatherForecastLayout.MeterWidth(276, 24, 160, 45, 88));

    [Fact]
    public void S12403_no_room_at_all_is_no_bar_never_a_negative_one()
        => Assert.Equal(0, WeatherForecastLayout.MeterWidth(40, 24, 60, 45, 0));

    [Fact]
    public void S726_columns_share_the_content_and_draw_every_part()
    {
        var plan = WeatherForecastLayout.Columns(new WidgetArea(304, 180), new WeatherForecastConfig { Style = ForecastStyle.Columns });

        Assert.Equal(56, plan.ColumnWidth, 6);
        Assert.Equal(FamilyShape.IconLarge, plan.Icon);
        Assert.True(plan.Low && plan.Bar && plan.Precipitation);
    }

    [Fact]
    public void S11145_lines_out_of_range_are_repaired_and_a_copy_keeps_them()
    {
        var config = new WeatherForecastConfig { Lines = 5 };
        Assert.Single(config.Repair());
        Assert.Equal(2, config.Lines);
        Assert.Equal(2, ((WeatherForecastConfig)config.Clone()).Lines);
    }

    [Fact]
    public void S6163_the_style_survives_a_clone()
        => Assert.Equal(ForecastStyle.Curve, ((WeatherForecastConfig)new WeatherForecastConfig { Style = ForecastStyle.Curve }.Clone()).Style);
}
