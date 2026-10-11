using System.Globalization;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-722]..[S-728] The weather widgets' disclosure ladders and readings, at the seven size
/// tiers (each card less its ~44px header).
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

    /// <summary>The reading at Subtitle with the condition under it.</summary>
    private const double StackedFloor = 28 + 20 + 4;

    private const double BesideFloor = 28;

    private static WeatherNowPlan Now(WidgetArea area, double floor = BesideFloor, bool details = true)
        => WeatherNowLayout.Plan(area, floor, facts: 10, details, hasToday: true);

    // ---- current conditions [S-723] ------------------------------------------------------

    [Fact]
    public void S723_the_smallest_tile_is_the_reading_alone()
    {
        var plan = Now(Xs, StackedFloor);

        Assert.Equal(WeatherDetailMode.None, plan.Detail);
        Assert.False(plan.HighLow);
    }

    [Fact]
    public void S723_a_small_tile_adds_one_feels_like_token()
        => Assert.Equal(WeatherDetailMode.Feels, Now(S, StackedFloor).Detail);

    [Fact]
    public void S723_a_medium_tile_shows_the_three_original_cells_and_today_s_range()
    {
        var plan = Now(M);

        Assert.Equal(WeatherDetailMode.Cells, plan.Detail);
        Assert.Equal(3, plan.FactCount);
        Assert.True(plan.HighLow);
    }

    [Fact]
    public void S723_a_strip_puts_the_readings_beside_the_number_not_under_it()
    {
        var plan = Now(Wide);

        Assert.Equal(WeatherDetailMode.Side, plan.Detail);
        Assert.InRange(plan.FactCount, 3, 10);
        Assert.True(plan.SideWidth > 0);
    }

    [Fact]
    public void S723_a_tall_column_stacks_the_readings_as_rows()
    {
        var plan = Now(Tall, StackedFloor);

        Assert.Equal(WeatherDetailMode.Stack, plan.Detail);
        Assert.InRange(plan.FactCount, 6, WeatherNowLayout.StackMax);
        Assert.True(plan.HighLow);
    }

    [Fact]
    public void S723_a_large_tile_has_no_hourly_chart_and_the_reading_takes_the_room()
    {
        // The next hours are the forecast widget's job (user decision, concise widgets): what the
        // cells and today's line leave goes to the reading, uncapped.
        var plan = Now(L);

        Assert.Equal(WeatherDetailMode.Cells, plan.Detail);
        Assert.True(plan.HeroRoom > 92 + 20 + 16, $"hero {plan.HeroRoom}");
    }

    [Fact]
    public void S723_the_largest_tile_adds_a_second_row_of_cells_and_no_next_days()
    {
        // The next days are the forecast widget's job: the largest tile gives the room to the
        // readings and the reading instead (user decision, concise widgets).
        var plan = Now(Xl);

        Assert.Equal(WeatherDetailMode.Cells, plan.Detail);
        Assert.Equal(10, plan.FactCount);
        Assert.True(plan.HeroRoom > Now(L).HeroRoom);
    }

    [Fact]
    public void S723_growing_a_square_tile_only_ever_adds_readings()
    {
        var previous = -1;

        foreach (var area in new[] { Xs, S, M, L, Xl })
        {
            var count = Now(area, StackedFloor).FactCount;
            Assert.True(count >= previous, $"{area}: {count} < {previous}");
            previous = count;
        }
    }

    [Fact]
    public void S723_the_show_detail_switch_off_is_the_reading_alone_at_every_size()
    {
        foreach (var area in new[] { Xs, S, M, Wide, Tall, L, Xl })
        {
            Assert.Equal(WeatherDetailMode.None, Now(area, details: false).Detail);
        }
    }

    [Fact]
    public void S723_an_unmeasured_area_draws_the_reading_alone()
        => Assert.Equal(WeatherDetailMode.None, Now(new WidgetArea(0, 0)).Detail);

    // ---- forecast [S-724]..[S-726] -------------------------------------------------------

    [Fact]
    public void S724_auto_is_columns_on_a_strip_and_rows_elsewhere()
    {
        Assert.Equal(ForecastLayout.Columns, WeatherForecastLayout.Resolve(ForecastLayout.Auto, Wide));
        Assert.Equal(ForecastLayout.Rows, WeatherForecastLayout.Resolve(ForecastLayout.Auto, M));
        Assert.Equal(ForecastLayout.Rows, WeatherForecastLayout.Resolve(ForecastLayout.Auto, Tall));
    }

    [Fact]
    public void S724_the_user_s_layout_always_wins()
    {
        Assert.Equal(ForecastLayout.Grid, WeatherForecastLayout.Resolve(ForecastLayout.Grid, Wide));
        Assert.Equal(ForecastLayout.Rows, WeatherForecastLayout.Resolve(ForecastLayout.Rows, Wide));
        Assert.Equal(ForecastLayout.Columns, WeatherForecastLayout.Resolve(ForecastLayout.Columns, Tall));
    }

    [Fact]
    public void S725_rows_stretch_to_fill_when_the_days_fit()
    {
        var plan = WeatherForecastLayout.Rows(Tall, 7);

        Assert.True(plan.Stretch);
        Assert.True(plan.RowHeight > WeatherForecastLayout.RowMin);
        Assert.True(plan.TwoLine, "a tall narrow row carries the sky on a second line");
    }

    [Fact]
    public void S725_more_days_than_fit_scroll_at_the_row_floor()
    {
        var plan = WeatherForecastLayout.Rows(M, 14);

        Assert.False(plan.Stretch);
        Assert.Equal(WeatherForecastLayout.RowMinCompact, plan.RowHeight);
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
        Assert.False(wide.Scroll);
    }

    [Fact]
    public void S726_columns_scroll_rather_than_squeeze_below_their_minimum()
    {
        var plan = WeatherForecastLayout.Columns(S, 14);

        Assert.True(plan.Scroll);
        Assert.Equal(WeatherForecastLayout.ColumnMin, plan.ColumnWidth);
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
    public void S726_the_grid_is_one_card_across_on_a_narrow_tile_and_more_on_a_wide_one()
    {
        Assert.Equal(1, WeatherForecastLayout.Grid(Tall, 7).Columns);
        Assert.Equal(2, WeatherForecastLayout.Grid(M, 7).Columns);
        Assert.Equal(4, WeatherForecastLayout.Grid(Xl, 7).Columns);
        Assert.Equal(2, WeatherForecastLayout.Grid(Xl, 7).Rows);
    }

    [Fact]
    public void S726_a_tall_card_adds_its_extras_and_a_short_one_sheds_them()
    {
        var big = WeatherForecastLayout.Grid(Xl, 7);
        Assert.True(big.Extras);
        Assert.True(big.Sun);

        var small = WeatherForecastLayout.Grid(S, 7);
        Assert.False(small.Description);
        Assert.False(small.Extras);
    }

    // ---- facts [S-722] -------------------------------------------------------------------

    [Fact]
    public void S722_the_original_three_readings_come_first_and_missing_ones_are_left_out()
    {
        var reading = new CurrentWeather(18, 17, 63, 11, 2) { WindDirection = 315, Pressure = null, UvIndex = 3 };

        var facts = WeatherFacts.For(Locale.En, reading, TemperatureUnit.Celsius);

        Assert.Equal(
            [WeatherFactKind.FeelsLike, WeatherFactKind.Humidity, WeatherFactKind.Wind, WeatherFactKind.Uv],
            facts.Select(fact => fact.Kind));
        Assert.Equal("11 km/h NW", facts[2].Value);
    }

    [Fact]
    public void S722_precipitation_is_the_amount_while_it_rains_and_today_s_chance_otherwise()
    {
        var today = new ForecastDay(new DateOnly(2026, 10, 4), 61, 9, 15, 3) { PrecipitationChance = 80 };
        var dry = new CurrentWeather(15, 14, 80, 5, 61) { Days = [today] };
        var wet = dry with { Precipitation = 0.6 };

        Assert.Equal("80%", Value(dry, WeatherFactKind.Precipitation));
        Assert.Equal("0.6 mm", Value(wet, WeatherFactKind.Precipitation));
        Assert.Equal("0,6 mm", Value(wet, WeatherFactKind.Precipitation, Locale.De));
    }

    [Fact]
    public void S722_sunrise_and_sunset_come_from_today_on_the_language_s_clock()
    {
        var today = new ForecastDay(new DateOnly(2026, 10, 4), 0, 9, 15, 0) { Sunrise = new TimeOnly(7, 31), Sunset = new TimeOnly(18, 54) };
        var reading = new CurrentWeather(15, 14, 50, 5, 0) { Days = [today] };

        Assert.Equal("7:31 AM", Value(reading, WeatherFactKind.Sunrise));
        Assert.Equal("6:54 PM", Value(reading, WeatherFactKind.Sunset));
        Assert.Equal("07:31", Value(reading, WeatherFactKind.Sunrise, Locale.De));
        Assert.Equal("18:54", Value(reading, WeatherFactKind.Sunset, Locale.De));
    }

    [Fact]
    public void S1100_the_readings_are_labelled_in_the_language_asked_for()
    {
        var reading = new CurrentWeather(18, 17, 63, 11, 2) { Pressure = 1013 };

        var labels = WeatherFacts.For(Locale.De, reading, TemperatureUnit.Celsius).Select(fact => fact.Label);

        Assert.Equal(["Gefühlt", "Luftfeuchte", "Wind", "Luftdruck"], labels);
    }

    private static string? Value(CurrentWeather reading, WeatherFactKind kind, Locale locale = Locale.En)
        => WeatherFacts.For(locale, reading, TemperatureUnit.Celsius)
            .Where(fact => fact.Kind == kind)
            .Select(fact => fact.Value)
            .FirstOrDefault();

    // ---- title [S-728] -------------------------------------------------------------------

    [Fact]
    public void S728_the_title_is_the_chosen_city_not_the_typed_query()
    {
        var config = new WeatherNowConfig { LocationName = "Zürich, Zurich, Switzerland", Latitude = 47.37, Longitude = 8.54 };

        Assert.Equal("Zürich", config.Title);
    }

    [Fact]
    public void S2846_with_no_place_picked_there_is_no_title_and_nothing_typed_stands_in()
        => Assert.Null(new WeatherForecastConfig().Title);

    [Fact]
    public void S728_hiding_the_location_leaves_the_header_to_the_widget_s_label()
    {
        var config = new WeatherNowConfig { LocationName = "Zürich, Zurich, Switzerland", HideLocation = true };

        Assert.Null(config.Title);
        Assert.True(((WeatherNowConfig)config.Clone()).HideLocation);
    }

    [Fact]
    public void S724_the_layout_survives_a_clone()
        => Assert.Equal(ForecastLayout.Grid, ((WeatherForecastConfig)new WeatherForecastConfig { Layout = ForecastLayout.Grid }.Clone()).Layout);
}
