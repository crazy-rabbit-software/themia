using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.PressureGraph.Tests;

/// <summary>[S-10700]..[S-10704] The pressure graph's mapping and config.</summary>
public sealed class PressureGraphTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    private static WeatherHours Hours(params double?[] values)
        => new([.. values.Select((v, i) => new WeatherHour(T0.AddHours(i - 1)) { Pressure = v })], []);

    private static HourGraphModel? Model(WeatherHours hours, PressureGraphConfig? config = null)
        => PressureGraphRules.Model(hours, config ?? new PressureGraphConfig { Hours = 12 }, T0.AddMinutes(20), Locale.En, TimeZoneInfo.Utc);

    [Fact]
    public void S10702_a_steady_day_is_drawn_flat_on_ten_hectopascals()
    {
        var model = Model(Hours(0, 1012, 1013))!;

        Assert.Equal(0.45, model.Slots[0].Value!.Value, 3);
        Assert.Equal(0.55, model.Slots[1].Value!.Value, 3);
        Assert.Equal("1012 hPa", model.Reading);
        Assert.Contains(new HourMark(1, "1013"), model.Marks);
        Assert.Contains(new HourMark(0, "1012", Under: true), model.Marks);
    }

    [Fact]
    public void S10702_gaps_and_too_few_hours()
    {
        Assert.Null(Model(Hours(1000, 1001, null, 1003))!.Slots[1].Value);
        Assert.Null(Model(Hours(1000, 1001, null, null)));
    }

    [Fact]
    public void S10701_the_area_look_and_a_clone()
    {
        var config = new PressureGraphConfig { Chart = ChartType.Area };

        Assert.Equal(HourGraphLook.Area, Model(Hours(1000, 1001, 1002), config)!.Look);
        Assert.Equal(HourGraphLook.Line, Model(Hours(1000, 1001, 1002))!.Look);
        Assert.Equal(ChartType.Area, ((PressureGraphConfig)config.Clone()).Chart);
    }
}
