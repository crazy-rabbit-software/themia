using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.WindGraph.Tests;

/// <summary>[S-10500]..[S-10504] The wind graph's mapping and config.</summary>
public sealed class WindGraphTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    private static WeatherHours Hours(params double?[] speeds)
        => new([.. speeds.Select((v, i) => new WeatherHour(T0.AddHours(i - 1)) { Wind = v, Gusts = v * 2, WindDirection = 225 })], []);

    private static HourGraphModel? Model(WeatherHours hours, WindGraphConfig? config = null)
        => WindGraphRules.Model(hours, config ?? new WindGraphConfig { Hours = 12 }, T0.AddMinutes(20), Locale.En, TimeZoneInfo.Utc);

    [Fact]
    public void S10500_its_glyph_is_the_wind_lines_the_icon_font_has()
    {
        // U+E9F2 is in neither Segoe Fluent Icons nor Segoe MDL2 Assets: the graph's top line and its tile drew a box.
        Assert.Equal(Themia.App.Shell.Glyphs.Wind, WindGraphModule.Glyph);
        Assert.NotEqual(char.ConvertFromUtf32(0xE9F2), new WindGraphModule().Descriptor.Glyph);
    }

    [Fact]
    public void S10502_speeds_and_gusts_on_one_scale_from_zero_with_the_directions_and_the_reading()
    {
        var model = Model(Hours(0, 10, 20, 15))!;

        Assert.Equal(1.0 / 4, model.Slots[0].Value!.Value, 3);
        Assert.Equal(1.0, model.Slots[1].Second!.Value, 3);
        Assert.True(model.Arrows);
        Assert.All(model.Slots, s => Assert.Equal(225, s.Direction));
        Assert.Equal(new HourMark(1, "20"), model.Marks.Single());
        Assert.Equal(WeatherReport.WindFrom(Locale.En, 10, 225), model.Reading);
    }

    [Fact]
    public void S10502_without_gusts_the_scale_is_the_speeds_at_least_30()
    {
        var model = Model(Hours(0, 6, 12), new WindGraphConfig { Hours = 12, ShowGusts = false })!;

        Assert.Equal(0.2, model.Slots[0].Value!.Value, 3);
        Assert.All(model.Slots, s => Assert.Null(s.Second));
    }

    [Fact]
    public void S10503_a_gale_is_caution_and_a_storm_critical()
    {
        var roles = Model(Hours(0, 61, 62, 88, 89))!.Slots.Select(s => s.Role);

        Assert.Equal([ColourRole.Accent, ColourRole.Caution, ColourRole.Caution, ColourRole.Critical], roles);
    }

    [Fact]
    public void S10502_gaps_and_too_few_hours()
    {
        Assert.Null(Model(Hours(1, 2, null, 3))!.Slots[1].Value);
        Assert.Null(Model(Hours(1, 2, null, null)));
    }

    [Fact]
    public void S10501_the_bars_look_and_a_clone()
    {
        var config = new WindGraphConfig { Chart = ChartType.Bars, ShowGusts = false };
        var copy = (WindGraphConfig)config.Clone();

        Assert.Equal(HourGraphLook.Bars, Model(Hours(1, 2, 3), config)!.Look);
        Assert.Equal((ChartType.Bars, false), (copy.Chart, copy.ShowGusts));
    }
}
