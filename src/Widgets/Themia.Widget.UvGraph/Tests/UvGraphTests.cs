using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.UvGraph.Tests;

/// <summary>[S-10300]..[S-10304] The UV graph's mapping and config.</summary>
public sealed class UvGraphTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    private static WeatherHours Hours(params double?[] uv)
        => new([.. uv.Select((v, i) => new WeatherHour(T0.AddHours(i - 1)) { Uv = v })], [new DaylightSpan(T0.AddHours(-2), T0.AddHours(8))]);

    private static HourGraphModel? Model(WeatherHours hours, UvGraphConfig? config = null)
        => UvGraphRules.Model(hours, config ?? new UvGraphConfig { Hours = 12 }, T0.AddMinutes(20), Locale.En, TimeZoneInfo.Utc);

    [Fact]
    public void S10303_each_band_draws_in_its_role_and_the_reading_names_the_band()
    {
        var model = Model(Hours(0, 2, 3, 6, 8, 11))!;

        Assert.Equal([ColourRole.Accent, ColourRole.Caution, ColourRole.Caution, ColourRole.Critical, ColourRole.Critical], model.Slots.Select(s => s.Role));
        Assert.Equal("2 · Low", model.Reading);
        Assert.Equal(ColourRole.TextPrimary, model.ReadingRole);
        Assert.Equal(ColourRole.Caution, Model(Hours(0, 7, 8))!.ReadingRole);
        Assert.Equal("7 · High", Model(Hours(0, 7, 8))!.Reading);
    }

    [Fact]
    public void S10302_the_peak_is_marked_on_a_scale_of_at_least_six_and_the_daylight_shaded()
    {
        var model = Model(Hours(0, 0, 1.5, 3, 1))!;

        Assert.Equal(new HourMark(2, "3"), model.Marks.Single());
        Assert.Equal(0.5, model.Slots[2].Value);
        Assert.Single(model.Shade);
        Assert.Equal(HourGraphLook.Bars, model.Look);
        Assert.Empty(Model(Hours(0, 0, 0))!.Marks);
    }

    [Fact]
    public void S10302_a_missing_hour_is_a_gap_and_too_few_draw_nothing()
    {
        Assert.Null(Model(Hours(1, 2, null, 3))!.Slots[1].Value);
        Assert.Null(Model(Hours(1, 2, null, null)));
    }

    [Fact]
    public void S10301_the_area_look_and_a_clone()
    {
        var config = new UvGraphConfig { Style = UvGraphStyle.Area, Hours = 48 };

        Assert.Equal(HourGraphLook.Area, Model(Hours(0, 1, 2), config)!.Look);
        Assert.Equal((UvGraphStyle.Area, 48), (((UvGraphConfig)config.Clone()).Style, ((UvGraphConfig)config.Clone()).Hours));
    }
}
