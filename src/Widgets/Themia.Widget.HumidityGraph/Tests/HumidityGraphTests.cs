using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.HumidityGraph.Tests;

/// <summary>[S-10600]..[S-10604] The humidity graph's mapping and config.</summary>
public sealed class HumidityGraphTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    private static WeatherHours Hours(params double?[] values)
        => new([.. values.Select((v, i) => new WeatherHour(T0.AddHours(i - 1)) { Humidity = v })], []);

    private static HourGraphModel? Model(WeatherHours hours, HumidityGraphConfig? config = null)
        => HumidityGraphRules.Model(hours, config ?? new HumidityGraphConfig { Hours = 12 }, T0.AddMinutes(20), Locale.En, TimeZoneInfo.Utc);

    [Fact]
    public void S10602_the_range_widens_to_twenty_points_inside_zero_to_a_hundred()
    {
        var model = Model(Hours(0, 95, 98))!;

        // 95..98 widens to 86.5..106.5, then is held under 100: 80..100.
        Assert.Equal(0.75, model.Slots[0].Value!.Value, 3);
        Assert.Equal(0.9, model.Slots[1].Value!.Value, 3);
        Assert.Equal("95%", model.Reading);
        Assert.All(model.Slots, s => Assert.Equal(ColourRole.Accent, s.Role));
    }

    [Fact]
    public void S10602_highest_over_lowest_under_gaps_and_too_few()
    {
        var model = Model(Hours(0, 60, null, 40, 80))!;

        Assert.Contains(new HourMark(3, "80%"), model.Marks);
        Assert.Contains(new HourMark(2, "40%", Under: true), model.Marks);
        Assert.Null(model.Slots[1].Value);
        Assert.Null(Model(Hours(1, 2, null, null)));
    }

    [Fact]
    public void S10601_the_line_look_and_a_clone()
    {
        var config = new HumidityGraphConfig { Style = HumidityGraphStyle.Line, Hours = 48 };

        Assert.Equal(HourGraphLook.Line, Model(Hours(1, 2, 3), config)!.Look);
        Assert.Equal(HourGraphLook.Area, Model(Hours(1, 2, 3))!.Look);
        Assert.Equal(48, ((HumidityGraphConfig)config.Clone()).Hours);
    }
}
