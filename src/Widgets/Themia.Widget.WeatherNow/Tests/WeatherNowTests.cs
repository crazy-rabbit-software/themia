using System.Globalization;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;
using static Themia.Core.Tests.StyleTiers;

namespace Themia.Widget.WeatherNow.Tests;

/// <summary>
/// [S-722] [S-723] [S-6161] [S-6162] The current conditions on the family's blocks: its readings, the
/// standard style's face (the detail widgets' plan) and its two other styles (the Readout's steps).
/// </summary>
public sealed class WeatherNowTests
{
    private static string? Value(CurrentWeather reading, WeatherFactKind kind, Locale locale = Locale.En)
        => WeatherFacts.For(locale, reading, TemperatureUnit.Celsius)
            .Where(fact => fact.Kind == kind)
            .Select(fact => fact.Value)
            .FirstOrDefault();


    // ---- [S-723] the standard style: the detail widgets' plan ------------------------------------

    [Fact]
    public void S723_the_small_tiles_are_the_reading_and_its_condition_and_what_whole_rows_fit()
    {
        foreach (var (tier, facts) in new[] { (Xs, 0), (S, 1) })
        {
            var plan = WeatherNowLayout.Plan(tier, facts: 11);
            Assert.Equal(facts, plan.Facts);
            Assert.True(plan.Under, $"{tier}: the condition under the reading");
            Assert.Equal(FamilyType.Big28, plan.Hero);
        }
    }

    [Fact]
    public void S723_a_medium_tile_puts_four_readings_under_the_reading_on_28()
    {
        var plan = WeatherNowLayout.Plan(M, facts: 11);

        Assert.False(plan.Side);
        Assert.Equal(FamilyType.Big28, plan.Hero);
        Assert.Equal(36 + 16 + 8, plan.RowsTop);
        Assert.Equal(4, plan.Facts);
    }

    [Fact]
    public void S723_an_expanded_width_puts_the_readings_beside_the_reading_in_two_fifths()
    {
        var wide = WeatherNowLayout.Plan(Wide, facts: 11);
        var xl = WeatherNowLayout.Plan(Xl, facts: 11);

        Assert.True(wide.Side);
        Assert.Equal(Math.Floor(480 * 0.4), wide.HeroColumn);
        Assert.Equal(4, wide.Facts);
        Assert.True(xl.Side);
        Assert.Equal(FamilyType.Big40, xl.Hero);
        Assert.Equal(11, xl.Facts);
    }

    [Fact]
    public void S723_the_reading_never_passes_40_and_a_tall_column_stacks_the_readings_under_it()
    {
        foreach (var tier in Tiers)
        {
            Assert.True(FamilyType.BaseOf(WeatherNowLayout.Plan(tier, facts: 11).Hero) <= 40, $"{tier}");
        }

        var tall = WeatherNowLayout.Plan(Tall, facts: 11);
        Assert.False(tall.Side);
        Assert.Equal(10, tall.Facts);
    }

    [Fact]
    public void S723_show_detail_off_is_the_reading_alone_at_every_size()
    {
        foreach (var tier in Tiers)
        {
            var plan = WeatherNowLayout.Plan(tier, facts: 11, details: false);
            Assert.Equal(0, plan.Facts);
            Assert.False(plan.Side);
        }
    }

    // ---- [S-6161] [S-6162] the other two styles ------------------------------------------------------

    [Fact]
    public void S6161_the_large_icon_is_a_family_glyph_no_taller_than_the_reading_beside_it()
    {
        var family = new double[] { 16, 28, 32, 40, 48 };
        foreach (var tier in Tiers)
        {
            var plan = WeatherNowStyles.LargeIcon(tier);
            var (width, _) = MeterPlan.Content(tier);
            Assert.Contains(plan.Glyph, family);
            Assert.True(plan.Glyph <= ReadoutRules.Height(plan.Temperature, WeatherNowStyles.Worst), $"{tier}: glyph {plan.Glyph}");
            Assert.True(plan.Glyph + 8 + ReadoutRules.Width(plan.Temperature, WeatherNowStyles.Worst) <= width + 0.01, $"{tier}: too wide");
        }

        Assert.Equal(48, WeatherNowStyles.LargeIcon(M).Glyph);
        Assert.Equal(FamilyType.Big28, WeatherNowStyles.LargeIcon(M).Temperature);
        Assert.Equal(28, WeatherNowStyles.LargeIcon(Xs).Glyph);
    }

    [Fact]
    public void S6162_the_minimal_temperature_climbs_the_readout_steps_and_stops_at_40()
    {
        Assert.Equal(FamilyType.Big28, WeatherNowStyles.Minimal(Xs));
        Assert.Equal(FamilyType.Big28, WeatherNowStyles.Minimal(M));
        Assert.Equal(FamilyType.Big40, WeatherNowStyles.Minimal(Xl));
    }

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
    public void S722_today_s_range_leads_the_readings_when_today_is_known()
    {
        var today = new ForecastDay(new DateOnly(2026, 10, 4), 2, 8, 16, 0);
        var reading = new CurrentWeather(14, 13, 71, 13, 2) { Days = [today] };

        var facts = WeatherFacts.For(Locale.En, reading, TemperatureUnit.Celsius);

        Assert.Equal(WeatherFactKind.Today, facts[0].Kind);
        Assert.Equal(("Today", "H 16°  L 8°"), (facts[0].Label, facts[0].Value));
        Assert.Equal(WeatherFactKind.FeelsLike, facts[1].Kind);
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
