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
        Assert.Equal((5, 1, ForecastStyle.Rows), (config.Days, config.Lines, config.Style));
    }

    [Fact]
    public void S11146_rows_are_a_row_a_day_their_height_stepped_by_the_days_220_to_420_wide()
    {
        var one = Kind.Sizing(new WeatherForecastConfig { Days = 5 });
        var two = Kind.Sizing(new WeatherForecastConfig { Days = 5, Lines = 2 });

        Assert.Equal(new SizeDip(300, 36 + (5 * 28) + 12), one.Natural);
        Assert.Equal(36 + (5 * 40) + 12, two.Natural.Height);
        Assert.Equal((220.0, 420.0), (one.Min.Width, one.Max.Width));
        Assert.True(one.HeightSteps!.FollowsConfig);
    }

    [Fact]
    public void S11146_columns_are_a_56_column_a_day_the_width_stepped_by_the_days_at_one_height()
    {
        var sizing = Kind.Sizing(new WeatherForecastConfig { Style = ForecastStyle.Columns, Days = 5 });
        var dry = Kind.Sizing(new WeatherForecastConfig { Style = ForecastStyle.Columns, Days = 5, ShowPrecipitation = false });

        Assert.Equal(24 + (5 * 56), sizing.Natural.Width);
        Assert.True(sizing.FixedHeight && sizing.WidthSteps!.FollowsConfig);
        Assert.Equal(36 + WeatherForecastLayout.ColumnHeight(true) + 12, sizing.Natural.Height);
        Assert.Equal(20, sizing.Natural.Height - dry.Natural.Height);
    }

    [Fact]
    public void S11146_dragging_the_rows_taller_or_the_columns_wider_sets_the_days()
    {
        var rows = new WeatherForecastConfig { Days = 5 };
        var columns = new WeatherForecastConfig { Style = ForecastStyle.Columns, Days = 5 };

        Assert.Equal(7, ((WeatherForecastConfig)Kind.SizeSets!(rows, new SizeDip(300, 36 + (7 * 28) + 12))).Days);
        Assert.Equal(3, ((WeatherForecastConfig)Kind.SizeSets(columns, new SizeDip(24 + (3 * 56), 200))).Days);
        Assert.Same(rows, Kind.SizeSets(rows, new SizeDip(400, 36 + (5 * 28) + 12)));
        Assert.Equal(5, rows.Days);
    }

    [Fact]
    public void S725_rows_show_the_configs_days_and_lines_and_the_bar_is_a_share_of_the_row()
    {
        var plan = WeatherForecastLayout.Rows(new WidgetArea(320, 200), new WeatherForecastConfig { Days = 6, Lines = 2 });

        Assert.Equal((6, 2, false), (plan.Days, plan.Lines, plan.Compact));
        Assert.Equal(Math.Floor((320 - 24) * WeatherForecastLayout.MeterShare), plan.Meter);
        Assert.Equal(WeatherForecastLayout.MeterMax, WeatherForecastLayout.Rows(new WidgetArea(800, 200), new WeatherForecastConfig()).Meter);
    }

    [Fact]
    public void S726_columns_share_the_content_and_draw_every_part()
    {
        var plan = WeatherForecastLayout.Columns(new WidgetArea(304, 180), new WeatherForecastConfig { Style = ForecastStyle.Columns, Days = 5 });

        Assert.Equal(56, plan.ColumnWidth, 6);
        Assert.Equal(FamilyShape.IconLarge, plan.Icon);
        Assert.True(plan.Low && plan.Bar && plan.Precipitation);
    }

    [Fact]
    public void S11145_days_and_lines_out_of_range_are_repaired_and_a_copy_keeps_them()
    {
        var config = new WeatherForecastConfig { Days = 40, Lines = 5 };
        Assert.Equal(2, config.Repair().Count);
        Assert.Equal((14, 2), (config.Days, config.Lines));
        Assert.Equal(2, ((WeatherForecastConfig)config.Clone()).Lines);
    }

    [Fact]
    public void S6163_the_style_survives_a_clone()
        => Assert.Equal(ForecastStyle.Curve, ((WeatherForecastConfig)new WeatherForecastConfig { Style = ForecastStyle.Curve }.Clone()).Style);
}
