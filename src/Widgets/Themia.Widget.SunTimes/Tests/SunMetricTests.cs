using System.Text.Json;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.SunTimes.Tests;

/// <summary>[S-11115] [S-14690] The sun's times, a single metric each: their values, their place, no trend.</summary>
public sealed class SunMetricTests
{
    /// <summary>Zurich at the summer solstice's midday (UTC+2).</summary>
    private static readonly DateTimeOffset Noon = new(2026, 6, 21, 12, 0, 0, TimeSpan.FromHours(2));

    [Fact]
    public void S11115_each_time_is_worked_out_from_the_sky_and_the_next_of_rise_and_set_is_known()
    {
        var rows = SunTimeRules.For([SunTimeKind.Dusk, SunTimeKind.Sunset, SunTimeKind.Sunrise], Noon, 47.37, 8.54);

        Assert.Equal([SunTimeKind.Dusk, SunTimeKind.Sunset, SunTimeKind.Sunrise], rows.Select(r => r.Kind));
        Assert.Equal([false, true, false], rows.Select(r => r.Next));
        Assert.All(rows, r => Assert.NotEqual("—", r.Value));
    }

    [Fact]
    public void S11115_inside_the_polar_circle_a_rise_or_set_that_does_not_happen_is_the_dash()
        => Assert.Equal(["—", "—"], SunTimeRules.For([SunTimeKind.Sunrise, SunTimeKind.Sunset], Noon, 78.2, 15.6).Select(r => r.Value));

    [Fact]
    public void S14690_a_time_card_keeps_its_place_and_has_no_trend()
    {
        var config = new SolarNoonConfig { Latitude = 46.2, Longitude = 6.1 };
        var back = (SolarNoonConfig)JsonSerializer.Deserialize(JsonSerializer.Serialize<WidgetConfig>(config, StateJson.WidgetConfig), StateJson.WidgetConfig)!;
        Assert.Equal((46.2, 6.1), (back.Latitude!.Value, back.Longitude!.Value));
        Assert.Equal(SunTimeKind.Noon, ((SolarNoonConfig)back.Clone()).Time);
        Assert.DoesNotContain(typeof(IMetricTrend), config.GetType().GetInterfaces());

        var menu = new WidgetMenuLines(() => { });
        ((IWidgetModule)new SunriseModule()).MenuLines(menu, new SunriseConfig());
        Assert.NotEmpty(menu.Lines);
        Assert.DoesNotContain(menu.Lines, l => l.Text == Str.Metric_Trend.Text());
    }
}
