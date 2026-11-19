using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.TemperatureGraph.Tests;

/// <summary>[S-10100]..[S-10104] The temperature graph's mapping and config.</summary>
public sealed class TemperatureGraphTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    private static WeatherHours Hours(params double?[] temps)
        => new([.. temps.Select((t, i) => new WeatherHour(T0.AddHours(i - 1)) { Temperature = t, FeelsLike = t - 2 })], []);

    private static HourGraphModel? Model(WeatherHours hours, TemperatureGraphConfig? config = null, TemperatureUnit unit = TemperatureUnit.Celsius)
        => TemperatureGraphRules.Model(hours, config ?? new TemperatureGraphConfig { Hours = 12 }, T0.AddMinutes(20), unit, Locale.En, TimeZoneInfo.Utc);

    [Fact]
    public void S10102_the_window_starts_at_the_hour_under_way_and_holds_its_hours()
    {
        var model = Model(Hours([.. Enumerable.Range(0, 50).Select(i => (double?)i)]))!;

        Assert.Equal(13, model.Slots.Count);
        Assert.Equal(T0, model.Slots[0].At);
        Assert.Equal("1°C", model.Reading);
        Assert.Equal(25, Model(Hours([.. Enumerable.Range(0, 50).Select(i => (double?)i)]), new TemperatureGraphConfig { Hours = 24 })!.Slots.Count);
    }

    [Fact]
    public void S10102_fahrenheit_follows_the_shared_unit()
    {
        var model = Model(Hours(0, 10, 20, 30), unit: TemperatureUnit.Fahrenheit)!;

        Assert.Equal("50°F", model.Reading);
        Assert.Contains(model.Marks, m => m.Text == "86°" && !m.Under);
        Assert.Contains(model.Marks, m => m.Text == "50°" && m.Under);
    }

    [Fact]
    public void S10102_a_missing_hour_is_a_gap_and_too_few_hours_draw_nothing()
    {
        var model = Model(Hours(5, 6, null, 8))!;

        Assert.Null(model.Slots[1].Value);
        Assert.NotNull(model.Slots[2].Value);
        Assert.Null(Model(Hours(5, 6, null, null)));
        Assert.Null(Model(new WeatherHours([], [])));
    }

    [Fact]
    public void S10102_the_feels_like_line_follows_its_switch()
    {
        Assert.All(Model(Hours(5, 6, 7))!.Slots, s => Assert.NotNull(s.Second));
        Assert.All(Model(Hours(5, 6, 7), new TemperatureGraphConfig { ShowFeelsLike = false })!.Slots, s => Assert.Null(s.Second));
    }

    [Fact]
    public void S10103_heat_is_caution_from_30_and_critical_from_35_in_either_unit()
    {
        var roles = Model(Hours(20, 29.9, 30, 34.9, 35), unit: TemperatureUnit.Fahrenheit)!.Slots.Select(s => s.Role);

        Assert.Equal([ColourRole.Accent, ColourRole.Caution, ColourRole.Caution, ColourRole.Critical], roles);
    }

    [Fact]
    public void S10101_the_chart_type_is_the_look_and_a_clone_keeps_every_setting()
    {
        var config = new TemperatureGraphConfig { Chart = ChartType.Line, ShowFeelsLike = false, Hours = 48, Latitude = 1, Longitude = 2 };
        var copy = (TemperatureGraphConfig)config.Clone();

        Assert.Equal(HourGraphLook.Line, Model(Hours(1, 2, 3), config)!.Look);
        Assert.Equal(HourGraphLook.Area, Model(Hours(1, 2, 3))!.Look);
        Assert.Equal((ChartType.Line, false, 48, 1.0), (copy.Chart, copy.ShowFeelsLike, copy.Hours, copy.Latitude!.Value));
    }
}
