using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.SunTimes;

[assembly: ThemiaWidgetFamily]
[assembly: ThemiaWidget("sunrise", typeof(SunriseModule))]
[assembly: ThemiaWidget("sunset", typeof(SunsetModule))]
[assembly: ThemiaWidget("daylight", typeof(DaylightModule))]
[assembly: ThemiaWidget("solarNoon", typeof(SolarNoonModule))]
[assembly: ThemiaWidget("dawn", typeof(DawnModule))]
[assembly: ThemiaWidget("dusk", typeof(DuskModule))]

namespace Themia.Widget.SunTimes;

/// <summary>
/// [S-14690] [S-14604] The family's kinds: the sun's times where the observer is, one widget a time (owner, 2026-11-20: "split all the other widgets
/// that are alike to the weather and air facts into individual metrics too"), in the removed card's order: each one's
/// id, the metric's key, its glyph, its history (its trends), its row's name, its name and description on the add page,
/// its search words and its config; each stands in for its row of a placed <c>sunTimes</c> card ([S-14606]).
/// </summary>
internal static class SunKinds
{
    public static IReadOnlyList<MetricKind> All { get; } = MetricKind.SplitOf("sunTimes",
    [
        new("sunrise", "Sunrise", Glyphs.Sun, MetricHistory.None, Str.WidgetUi_Sunrise.Local(), SunTimesStr.SunTimes_Sunrise_Label.Local(), SunTimesStr.SunTimes_Sunrise_Desc.Local(),
            ["sunrise", "dawn", "morning", "sun"], () => new SunriseConfig()),
        new("sunset", "Sunset", Glyphs.Sun, MetricHistory.None, Str.WidgetUi_Sunset.Local(), SunTimesStr.SunTimes_Sunset_Label.Local(), SunTimesStr.SunTimes_Sunset_Desc.Local(),
            ["sunset", "dusk", "evening", "sun"], () => new SunsetConfig()),
        new("daylight", "Daylight", Glyphs.Clock, MetricHistory.None, SunTimesStr.WidgetUi_Daylight.Local(), SunTimesStr.SunTimes_Daylight_Label.Local(), SunTimesStr.SunTimes_Daylight_Desc.Local(),
            ["daylight", "day length", "hours of daylight", "sun"], () => new DaylightConfig()),
        new("solarNoon", "Noon", Glyphs.Sun, MetricHistory.None, SunTimesStr.WidgetsC_SunMoon_SolarNoon.Local(), SunTimesStr.SunTimes_SolarNoon_Label.Local(), SunTimesStr.SunTimes_SolarNoon_Desc.Local(),
            ["solar noon", "midday", "noon", "sun"], () => new SolarNoonConfig()),
        new("dawn", "Dawn", Glyphs.Sun, MetricHistory.None, SunTimesStr.WidgetsC_SunMoon_Dawn.Local(), SunTimesStr.SunTimes_Dawn_Label.Local(), SunTimesStr.SunTimes_Dawn_Desc.Local(),
            ["dawn", "twilight", "first light", "sun"], () => new DawnConfig()),
        new("dusk", "Dusk", Glyphs.Moon, MetricHistory.None, SunTimesStr.WidgetsC_SunMoon_Dusk.Local(), SunTimesStr.SunTimes_Dusk_Label.Local(), SunTimesStr.SunTimes_Dusk_Desc.Local(),
            ["dusk", "twilight", "last light", "sun"], () => new DuskConfig()),
    ], ["Sunrise", "Sunset", "Daylight"]);

    /// <summary>The kind showing the metric <paramref name="key"/>.</summary>
    public static MetricKind Of(string key) => All.Single(k => k.Key == key);

    /// <summary>Its place in the family's order: its add-page rank.</summary>
    public static int Rank(string key) => All.ToList().FindIndex(k => k.Key == key);
}

// [S-14604] Each kind is its module and its config (a config's type is how it knows its id, [S-6902]); everything
// else is the family's (SunMetricModule, SunMetricConfig, the view) over the single metric's base.

public sealed class SunriseModule() : SunMetricModule("Sunrise");

public sealed class SunsetModule() : SunMetricModule("Sunset");

public sealed class DaylightModule() : SunMetricModule("Daylight");

public sealed class SolarNoonModule() : SunMetricModule("Noon");

public sealed class DawnModule() : SunMetricModule("Dawn");

public sealed class DuskModule() : SunMetricModule("Dusk");

public sealed class SunriseConfig() : SunMetricConfig("Sunrise");

public sealed class SunsetConfig() : SunMetricConfig("Sunset");

public sealed class DaylightConfig() : SunMetricConfig("Daylight");

public sealed class SolarNoonConfig() : SunMetricConfig("Noon");

public sealed class DawnConfig() : SunMetricConfig("Dawn");

public sealed class DuskConfig() : SunMetricConfig("Dusk");
