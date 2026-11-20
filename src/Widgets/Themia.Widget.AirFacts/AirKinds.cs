using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.AirFacts;

[assembly: ThemiaWidgetFamily]
[assembly: ThemiaWidget("airUv", typeof(AirUvModule))]
[assembly: ThemiaWidget("airPm25", typeof(AirPm25Module))]
[assembly: ThemiaWidget("airPollen", typeof(AirPollenModule))]
[assembly: ThemiaWidget("airPm10", typeof(AirPm10Module))]
[assembly: ThemiaWidget("airOzone", typeof(AirOzoneModule))]
[assembly: ThemiaWidget("airAlder", typeof(AirAlderModule))]
[assembly: ThemiaWidget("airBirch", typeof(AirBirchModule))]
[assembly: ThemiaWidget("airGrass", typeof(AirGrassModule))]
[assembly: ThemiaWidget("airMugwort", typeof(AirMugwortModule))]
[assembly: ThemiaWidget("airOlive", typeof(AirOliveModule))]
[assembly: ThemiaWidget("airRagweed", typeof(AirRagweedModule))]

namespace Themia.Widget.AirFacts;

/// <summary>
/// [S-14680] [S-14604] The family's kinds: the air's readings where the user says, one widget a reading (owner, 2026-11-20: "split all the other widgets
/// that are alike to the weather and air facts into individual metrics too"), in the removed card's order: each one's
/// id, the metric's key, its glyph, its history (its trends), its row's name, its name and description on the add page,
/// its search words and its config; each stands in for its row of a placed <c>airFacts</c> card ([S-14606]).
/// </summary>
internal static class AirKinds
{
    public static IReadOnlyList<MetricKind> All { get; } = MetricKind.SplitOf("airFacts",
    [
        new("airUv", "Uv", Glyphs.Sun, MetricHistory.Readings, AirFactsStr.Online_Air_Uv.Local(), AirFactsStr.AirFacts_AirUv_Label.Local(), AirFactsStr.AirFacts_AirUv_Desc.Local(),
            ["uv", "uv index", "sunburn", "sun", "air"], () => new AirUvConfig()),
        new("airPm25", "Pm25", Glyphs.Fog, MetricHistory.Series, AirFactsStr.Online_Air_Pm25.Local(), AirFactsStr.AirFacts_AirPm25_Label.Local(), AirFactsStr.AirFacts_AirPm25_Desc.Local(),
            ["pm2.5", "fine dust", "particulates", "air quality", "air"], () => new AirPm25Config()),
        new("airPollen", "Pollen", AirFactRules.Leaf, MetricHistory.None, AirFactsStr.Online_Air_Pollen.Local(), AirFactsStr.AirFacts_AirPollen_Label.Local(), AirFactsStr.AirFacts_AirPollen_Desc.Local(),
            ["pollen", "allergy", "hay fever", "air"], () => new AirPollenConfig()),
        new("airPm10", "Pm10", Glyphs.Fog, MetricHistory.Series, AirFactsStr.Online_Air_Pm10.Local(), AirFactsStr.AirFacts_AirPm10_Label.Local(), AirFactsStr.AirFacts_AirPm10_Desc.Local(),
            ["pm10", "dust", "particulates", "air quality", "air"], () => new AirPm10Config()),
        new("airOzone", "Ozone", Glyphs.Globe, MetricHistory.Series, AirFactsStr.Online_Air_Ozone.Local(), AirFactsStr.AirFacts_AirOzone_Label.Local(), AirFactsStr.AirFacts_AirOzone_Desc.Local(),
            ["ozone", "o3", "smog", "air quality", "air"], () => new AirOzoneConfig()),
        new("airAlder", "Alder", AirFactRules.Leaf, MetricHistory.Readings, AirFactsStr.Online_Air_PollenType_Alder.Local(), AirFactsStr.AirFacts_AirAlder_Label.Local(), AirFactsStr.AirFacts_AirAlder_Desc.Local(),
            ["alder", "pollen", "allergy", "hay fever"], () => new AirAlderConfig()),
        new("airBirch", "Birch", AirFactRules.Leaf, MetricHistory.Readings, AirFactsStr.Online_Air_PollenType_Birch.Local(), AirFactsStr.AirFacts_AirBirch_Label.Local(), AirFactsStr.AirFacts_AirBirch_Desc.Local(),
            ["birch", "pollen", "allergy", "hay fever"], () => new AirBirchConfig()),
        new("airGrass", "Grass", AirFactRules.Leaf, MetricHistory.Readings, AirFactsStr.Online_Air_PollenType_Grass.Local(), AirFactsStr.AirFacts_AirGrass_Label.Local(), AirFactsStr.AirFacts_AirGrass_Desc.Local(),
            ["grass", "pollen", "allergy", "hay fever"], () => new AirGrassConfig()),
        new("airMugwort", "Mugwort", AirFactRules.Leaf, MetricHistory.Readings, AirFactsStr.Online_Air_PollenType_Mugwort.Local(), AirFactsStr.AirFacts_AirMugwort_Label.Local(), AirFactsStr.AirFacts_AirMugwort_Desc.Local(),
            ["mugwort", "pollen", "allergy", "hay fever"], () => new AirMugwortConfig()),
        new("airOlive", "Olive", AirFactRules.Leaf, MetricHistory.Readings, AirFactsStr.Online_Air_PollenType_Olive.Local(), AirFactsStr.AirFacts_AirOlive_Label.Local(), AirFactsStr.AirFacts_AirOlive_Desc.Local(),
            ["olive", "pollen", "allergy", "hay fever"], () => new AirOliveConfig()),
        new("airRagweed", "Ragweed", AirFactRules.Leaf, MetricHistory.Readings, AirFactsStr.Online_Air_PollenType_Ragweed.Local(), AirFactsStr.AirFacts_AirRagweed_Label.Local(), AirFactsStr.AirFacts_AirRagweed_Desc.Local(),
            ["ragweed", "pollen", "allergy", "hay fever"], () => new AirRagweedConfig()),
    ], ["Uv", "Pm25", "Pollen", "Pm10"]);

    /// <summary>The kind showing the metric <paramref name="key"/>.</summary>
    public static MetricKind Of(string key) => All.Single(k => k.Key == key);

    /// <summary>Its place in the family's order: its add-page rank.</summary>
    public static int Rank(string key) => All.ToList().FindIndex(k => k.Key == key);
}

// [S-14604] Each kind is its module and its config (a config's type is how it knows its id, [S-6902]); everything
// else is the family's (AirMetricModule, AirMetricConfig, the view) over the single metric's base.

public sealed class AirUvModule() : AirMetricModule("Uv");

public sealed class AirPm25Module() : AirMetricModule("Pm25");

public sealed class AirPollenModule() : AirMetricModule("Pollen");

public sealed class AirPm10Module() : AirMetricModule("Pm10");

public sealed class AirOzoneModule() : AirMetricModule("Ozone");

public sealed class AirAlderModule() : AirMetricModule("Alder");

public sealed class AirBirchModule() : AirMetricModule("Birch");

public sealed class AirGrassModule() : AirMetricModule("Grass");

public sealed class AirMugwortModule() : AirMetricModule("Mugwort");

public sealed class AirOliveModule() : AirMetricModule("Olive");

public sealed class AirRagweedModule() : AirMetricModule("Ragweed");

public sealed class AirUvConfig() : AirMetricConfig("Uv");

public sealed class AirPm25Config() : AirMetricConfig("Pm25");

public sealed class AirPollenConfig() : AirMetricConfig("Pollen");

public sealed class AirPm10Config() : AirMetricConfig("Pm10");

public sealed class AirOzoneConfig() : AirMetricConfig("Ozone");

public sealed class AirAlderConfig() : AirMetricConfig("Alder");

public sealed class AirBirchConfig() : AirMetricConfig("Birch");

public sealed class AirGrassConfig() : AirMetricConfig("Grass");

public sealed class AirMugwortConfig() : AirMetricConfig("Mugwort");

public sealed class AirOliveConfig() : AirMetricConfig("Olive");

public sealed class AirRagweedConfig() : AirMetricConfig("Ragweed");
