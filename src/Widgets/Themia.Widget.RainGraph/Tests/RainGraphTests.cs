using Themia.Core.Localization;
using Themia.Core.Sync;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.RainGraph.Tests;

/// <summary>[S-10200]..[S-10204] The rain graph's mapping, its two looks' wants and its config.</summary>
public sealed class RainGraphTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    private static WeatherHours Hours(params double?[] amounts)
        => new([.. amounts.Select((a, i) => new WeatherHour(T0.AddHours(i - 1)) { Precipitation = a, PrecipitationChance = 50 })], []);

    private static HourGraphModel? Model(WeatherHours hours, RainGraphConfig? config = null)
        => RainGraphRules.Hours(hours, config ?? new RainGraphConfig { Hours = 12 }, T0.AddMinutes(20), Locale.En, TimeZoneInfo.Utc);

    [Fact]
    public void S10202_columns_from_zero_on_at_least_two_millimetres_the_wettest_marked_and_the_total_read()
    {
        var model = Model(Hours(9, 0, 0.5, 1.0, 0))!;

        Assert.Equal(HourGraphLook.Bars, model.Look);
        Assert.Equal([0, 0.25, 0.5, 0], model.Slots.Select(s => s.Value!.Value));
        Assert.Equal(new HourMark(2, "1.0"), model.Marks.Single());
        Assert.Equal("1.5 mm", model.Reading);
        Assert.All(model.Slots, s => Assert.Equal(0.5, s.Second));
    }

    [Fact]
    public void S10202_a_dry_window_reads_dry_and_marks_nothing_and_no_amount_draws_nothing()
    {
        var model = Model(Hours(0, 0, 0))!;

        Assert.Equal("Dry", model.Reading);
        Assert.Empty(model.Marks);
        Assert.Null(Model(Hours(null, null, null)));
    }

    [Fact]
    public void S10202_heavy_hours_are_caution_and_the_chance_line_follows_its_switch()
    {
        var model = Model(Hours(0, 2, 7.6, 60), new RainGraphConfig { Hours = 12, ShowChance = false })!;

        Assert.Equal([ColourRole.Accent, ColourRole.Caution, ColourRole.Critical], model.Slots.Select(s => s.Role));
        Assert.All(model.Slots, s => Assert.Null(s.Second));
    }

    [Fact]
    public void S10203_the_nowcast_is_the_rain_widgets_window_by_the_quarter()
    {
        var steps = Enumerable.Range(1, 10).Select(i => new RainStep(T0.AddMinutes(15 * i), i is 3 or 4 ? 0.6 : 0)).ToList();
        var model = RainGraphRules.Nowcast(new RainNowcast(steps, []), new RainGraphConfig(), T0.AddMinutes(5), Locale.En, TimeZoneInfo.Utc)!;

        Assert.Equal(RainRules.WindowSteps, model.Slots.Count);
        Assert.Equal(TimeSpan.FromMinutes(15), model.Step);
        Assert.Equal(T0, model.Slots[0].At);
        Assert.Equal("1.2 mm", model.Reading);
        Assert.Equal(LocaleFormat.Time(Locale.En, new TimeOnly(11, 0)), model.EndLabel);
    }

    [Fact]
    public void S10203_each_look_asks_for_its_own_answer_only()
    {
        var hours = new RainGraphConfig { Latitude = 47.37, Longitude = 8.54 };
        var nowcast = new RainGraphConfig { Latitude = 47.37, Longitude = 8.54, Style = RainGraphStyle.Nowcast };
        var a = new InterestsBuilder(new RefreshPreferences(), TemperatureUnit.Celsius);
        hours.AddInterests(a);
        nowcast.AddInterests(a);

        Assert.Single(a.Of<HoursInterest>());
        Assert.Single(a.Of<RainInterest>());
        Assert.Null(hours.Nowcast());
        Assert.Null(nowcast.Hourly());
        Assert.Equal((RainGraphStyle.Nowcast, true), (((RainGraphConfig)nowcast.Clone()).Style, ((RainGraphConfig)nowcast.Clone()).ShowChance));
    }
}
