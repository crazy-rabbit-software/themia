using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.PingFacts;

[assembly: ThemiaWidgetFamily]
[assembly: ThemiaWidget("pingAverage", typeof(PingAverageModule))]
[assembly: ThemiaWidget("pingMinimum", typeof(PingMinimumModule))]
[assembly: ThemiaWidget("pingMaximum", typeof(PingMaximumModule))]
[assembly: ThemiaWidget("pingJitter", typeof(PingJitterModule))]
[assembly: ThemiaWidget("pingLoss", typeof(PingLossModule))]
[assembly: ThemiaWidget("pingAnswered", typeof(PingAnsweredModule))]

namespace Themia.Widget.PingFacts;

/// <summary>
/// [S-14650] [S-14604] The family's kinds: a host's latency statistics, one widget a figure (owner, 2026-11-20: "split all the other widgets
/// that are alike to the weather and air facts into individual metrics too"), in the removed card's order: each one's
/// id, the metric's key, its glyph, its history (its trends), its row's name, its name and description on the add page,
/// its search words and its config.
/// </summary>
internal static class PingKinds
{
    public static IReadOnlyList<MetricKind> All { get; } = MetricKind.Opening(
    [
        new("pingAverage", "average", Glyphs.Ping, MetricHistory.Series, PingFactsStr.Details_Ping_Average.Local(), PingFactsStr.PingFacts_PingAverage_Label.Local(), PingFactsStr.PingFacts_PingAverage_Desc.Local(),
            ["ping", "latency", "average", "ms", "connection quality"], () => new PingAverageConfig()),
        new("pingMinimum", "minimum", Glyphs.Ping, MetricHistory.Series, PingFactsStr.Details_Ping_Minimum.Local(), PingFactsStr.PingFacts_PingMinimum_Label.Local(), PingFactsStr.PingFacts_PingMinimum_Desc.Local(),
            ["ping", "latency", "minimum", "fastest", "ms"], () => new PingMinimumConfig()),
        new("pingMaximum", "maximum", Glyphs.Ping, MetricHistory.Series, PingFactsStr.Details_Ping_Maximum.Local(), PingFactsStr.PingFacts_PingMaximum_Label.Local(), PingFactsStr.PingFacts_PingMaximum_Desc.Local(),
            ["ping", "latency", "maximum", "slowest", "ms"], () => new PingMaximumConfig()),
        new("pingJitter", "jitter", Glyphs.Ping, MetricHistory.Series, PingFactsStr.Details_Ping_Jitter.Local(), PingFactsStr.PingFacts_PingJitter_Label.Local(), PingFactsStr.PingFacts_PingJitter_Desc.Local(),
            ["ping", "jitter", "latency", "variation", "ms"], () => new PingJitterConfig()),
        new("pingLoss", "loss", Glyphs.Ping, MetricHistory.Series, PingFactsStr.Details_Ping_Loss.Local(), PingFactsStr.PingFacts_PingLoss_Label.Local(), PingFactsStr.PingFacts_PingLoss_Desc.Local(),
            ["ping", "packet loss", "loss", "dropped"], () => new PingLossConfig()),
        new("pingAnswered", "answered", Glyphs.Ping, MetricHistory.None, PingFactsStr.Details_Ping_Answered.Local(), PingFactsStr.PingFacts_PingAnswered_Label.Local(), PingFactsStr.PingFacts_PingAnswered_Desc.Local(),
            ["ping", "answered", "replies"], () => new PingAnsweredConfig()),
    ], MetricOpens.Network);

    /// <summary>The kind showing the metric <paramref name="key"/>.</summary>
    public static MetricKind Of(string key) => All.Single(k => k.Key == key);

    /// <summary>Its place in the family's order: its add-page rank.</summary>
    public static int Rank(string key) => All.ToList().FindIndex(k => k.Key == key);
}

// [S-14604] Each kind is its module and its config (a config's type is how it knows its id, [S-6902]); everything
// else is the family's (PingMetricModule, PingMetricConfig, the view) over the single metric's base.

public sealed class PingAverageModule() : PingMetricModule("average");

public sealed class PingMinimumModule() : PingMetricModule("minimum");

public sealed class PingMaximumModule() : PingMetricModule("maximum");

public sealed class PingJitterModule() : PingMetricModule("jitter");

public sealed class PingLossModule() : PingMetricModule("loss");

public sealed class PingAnsweredModule() : PingMetricModule("answered");

public sealed class PingAverageConfig() : PingMetricConfig("average");

public sealed class PingMinimumConfig() : PingMetricConfig("minimum");

public sealed class PingMaximumConfig() : PingMetricConfig("maximum");

public sealed class PingJitterConfig() : PingMetricConfig("jitter");

public sealed class PingLossConfig() : PingMetricConfig("loss");

public sealed class PingAnsweredConfig() : PingMetricConfig("answered");
