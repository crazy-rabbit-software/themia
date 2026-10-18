using System.Globalization;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Xunit;
using static Themia.Core.Tests.StyleTiers;

namespace Themia.Widget.WeatherNow.Tests;

/// <summary>
/// [S-722] [S-723] [S-6161] [S-6162] The current conditions: its readings, its ladder and its two
/// other styles. Moved with the widget from Core's WeatherLayoutTests and WeatherStylesTests,
/// unchanged in what they assert (the readings' labels in a language, once S1100, are its S722).
/// </summary>
public sealed class WeatherNowTests
{
    /// <summary>The reading at Subtitle with the condition under it.</summary>
    private const double StackedFloor = 28 + 20 + 4;

    private const double BesideFloor = 28;

    private static string? Value(CurrentWeather reading, WeatherFactKind kind, Locale locale = Locale.En)
        => WeatherFacts.For(locale, reading, TemperatureUnit.Celsius)
            .Where(fact => fact.Kind == kind)
            .Select(fact => fact.Value)
            .FirstOrDefault();


    [Fact]
    public void S6161_the_large_icon_stands_beside_the_temperature_and_above_it_on_a_column()
    {
        var medium = WeatherNowStyles.LargeIcon(M, hasToday: true);
        var tall = WeatherNowStyles.LargeIcon(Tall, hasToday: true);

        Assert.False(medium.Stacked);
        Assert.True(medium.Condition);
        Assert.True(medium.IconSize >= 64, $"icon {medium.IconSize}");
        Assert.True(tall.Stacked);
        Assert.True(tall.IconSize >= 64, $"icon {tall.IconSize}");
    }

    [Fact]
    public void S6161_the_large_icon_and_the_temperature_fit_the_width_at_every_tier()
    {
        foreach (var tier in Tiers)
        {
            var plan = WeatherNowStyles.LargeIcon(tier, hasToday: true);
            var box = tier.Width - (2 * WeatherNowLayout.Inset);
            var reading = HeroLayout.ValueWidth(plan.Temperature, WeatherNowStyles.ValueCharacters, WeatherNowStyles.UnitCharacters);
            var used = plan.Stacked ? Math.Max(plan.IconSize, reading) : plan.IconSize + WeatherNowStyles.IconGap + reading;

            Assert.True(plan.IconSize >= WeatherNowStyles.IconMin, $"{tier}: icon {plan.IconSize}");
            Assert.True(used <= box + 1, $"{tier}: {used} of {box}");
        }
    }

    [Fact]
    public void S6161_today_s_range_comes_only_with_the_condition_and_only_when_known()
    {
        Assert.False(WeatherNowStyles.LargeIcon(L, hasToday: false).HighLow);
        Assert.True(WeatherNowStyles.LargeIcon(L, hasToday: true).HighLow);
        Assert.False(WeatherNowStyles.LargeIcon(Xs, hasToday: true).HighLow);
    }

    [Fact]
    public void S6162_the_minimal_temperature_grows_with_the_tile_and_past_display_on_a_large_one()
    {
        var steps = new[] { Xs, S, M, L, Xl }.Select(WeatherNowStyles.Minimal).ToList();

        for (var i = 1; i < steps.Count; i++)
        {
            Assert.True(steps[i].FontSize >= steps[i - 1].FontSize);
        }

        Assert.True(steps[^1].FontSize > FluentType.Display.FontSize);

        // And it still fits: the reading's width and line inside the inset box.
        var box = Xl.Deflate(WeatherNowLayout.Inset, WeatherNowLayout.VerticalInset);
        Assert.True(HeroLayout.ValueWidth(steps[^1], WeatherNowStyles.ValueCharacters, WeatherNowStyles.UnitCharacters) <= box.Width + 1);
        Assert.True(steps[^1].LineHeight <= box.Height + 1);
    }

    private static WeatherNowPlan Now(WidgetArea area, double floor = BesideFloor, bool details = true)
        => WeatherNowLayout.Plan(area, floor, facts: 10, details, hasToday: true);

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
    public void S723_a_large_tile_puts_the_readings_beside_the_reading_not_under_a_dead_band()
    {
        // [S-668] Under the reading the cells left a band of nothing between the number and them at
        // L; beside it they are one column of stat lines and the reading keeps the height.
        var plan = Now(L);

        Assert.Equal(WeatherDetailMode.Side, plan.Detail);
        Assert.Equal(1, plan.FactColumns);
        Assert.Equal(10, plan.FactCount);
        Assert.True(plan.HighLow);
        Assert.True(plan.HeroRoom > 92 + 20 + 16, $"hero {plan.HeroRoom}");
    }

    [Fact]
    public void S723_the_largest_tile_shows_every_reading_and_no_next_days()
    {
        // The next days are the forecast widget's job: the largest tile gives the room to the
        // readings and the reading instead (user decision, concise widgets).
        var plan = Now(Xl);

        Assert.Equal(WeatherDetailMode.Side, plan.Detail);
        Assert.Equal(10, plan.FactCount);
        Assert.True(plan.HeroRoom >= Now(L).HeroRoom);
    }

    [Theory]
    [InlineData(560, 376)]
    [InlineData(800, 516)]
    [InlineData(480, 116)]
    public void S723_the_readings_beside_never_overrun_the_tile(double width, double height)
    {
        var plan = Now(new WidgetArea(width, height));
        var perColumn = (plan.FactCount + plan.FactColumns - 1) / plan.FactColumns;

        Assert.True(perColumn * WeatherNowLayout.StatLine <= height - (2 * WeatherNowLayout.VerticalInset) + 0.5);
    }

    [Fact]
    public void S723_a_medium_tile_keeps_its_cells_under_the_reading()
        => Assert.Equal(WeatherDetailMode.Cells, Now(M).Detail);

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
    public void S722_the_readings_are_labelled_in_the_language_asked_for()
    {
        var reading = new CurrentWeather(18, 17, 63, 11, 2) { Pressure = 1013 };

        var labels = WeatherFacts.For(Locale.De, reading, TemperatureUnit.Celsius).Select(fact => fact.Label);

        Assert.Equal(["Gefühlt", "Luftfeuchte", "Wind", "Luftdruck"], labels);
    }
}
