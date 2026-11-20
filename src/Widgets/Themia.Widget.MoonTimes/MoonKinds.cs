using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.MoonTimes;

[assembly: ThemiaWidgetFamily]
[assembly: ThemiaWidget("moonrise", typeof(MoonriseModule))]
[assembly: ThemiaWidget("moonset", typeof(MoonsetModule))]
[assembly: ThemiaWidget("nextFullMoon", typeof(NextFullMoonModule))]
[assembly: ThemiaWidget("nextNewMoon", typeof(NextNewMoonModule))]

namespace Themia.Widget.MoonTimes;

/// <summary>
/// [S-14695] [S-14604] The family's kinds: the moon's times where the observer is, one widget a time (owner, 2026-11-20: "split all the other widgets
/// that are alike to the weather and air facts into individual metrics too"), in the removed card's order: each one's
/// id, the metric's key, its glyph, its history (its trends), its row's name, its name and description on the add page,
/// its search words and its config; each stands in for its row of a placed <c>moonTimes</c> card ([S-14606]).
/// </summary>
internal static class MoonKinds
{
    public static IReadOnlyList<MetricKind> All { get; } = MetricKind.SplitOf("moonTimes",
    [
        new("moonrise", "Rise", Glyphs.Moon, MetricHistory.None, MoonTimesStr.WidgetsC_Moon_Rise.Local(), MoonTimesStr.MoonTimes_Moonrise_Label.Local(), MoonTimesStr.MoonTimes_Moonrise_Desc.Local(),
            ["moonrise", "moon", "rise"], () => new MoonriseConfig()),
        new("moonset", "Set", Glyphs.Moon, MetricHistory.None, MoonTimesStr.WidgetsC_Moon_Set.Local(), MoonTimesStr.MoonTimes_Moonset_Label.Local(), MoonTimesStr.MoonTimes_Moonset_Desc.Local(),
            ["moonset", "moon", "set"], () => new MoonsetConfig()),
        new("nextFullMoon", "NextFull", Glyphs.Moon, MetricHistory.None, MoonTimesStr.MoonTimes_NextFullMoon_Label.Local(), MoonTimesStr.MoonTimes_NextFullMoon_Label.Local(), MoonTimesStr.MoonTimes_NextFullMoon_Desc.Local(),
            ["full moon", "moon", "phase", "lunar"], () => new NextFullMoonConfig()),
        new("nextNewMoon", "NextNew", Glyphs.Moon, MetricHistory.None, MoonTimesStr.MoonTimes_NextNewMoon_Label.Local(), MoonTimesStr.MoonTimes_NextNewMoon_Label.Local(), MoonTimesStr.MoonTimes_NextNewMoon_Desc.Local(),
            ["new moon", "moon", "phase", "lunar"], () => new NextNewMoonConfig()),
    ], ["Rise", "Set", "NextFull"]);

    /// <summary>The kind showing the metric <paramref name="key"/>.</summary>
    public static MetricKind Of(string key) => All.Single(k => k.Key == key);

    /// <summary>Its place in the family's order: its add-page rank.</summary>
    public static int Rank(string key) => All.ToList().FindIndex(k => k.Key == key);
}

// [S-14604] Each kind is its module and its config (a config's type is how it knows its id, [S-6902]); everything
// else is the family's (MoonMetricModule, MoonMetricConfig, the view) over the single metric's base.

public sealed class MoonriseModule() : MoonMetricModule("Rise");

public sealed class MoonsetModule() : MoonMetricModule("Set");

public sealed class NextFullMoonModule() : MoonMetricModule("NextFull");

public sealed class NextNewMoonModule() : MoonMetricModule("NextNew");

public sealed class MoonriseConfig() : MoonMetricConfig("Rise");

public sealed class MoonsetConfig() : MoonMetricConfig("Set");

public sealed class NextFullMoonConfig() : MoonMetricConfig("NextFull");

public sealed class NextNewMoonConfig() : MoonMetricConfig("NextNew");
