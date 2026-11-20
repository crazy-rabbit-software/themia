using System.Text.Json;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Modules.Tests.Modules;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-14700]..[S-14707] Every single metric's kind: its graph's chart look (owner, 2026-11-20: "for the trendline you should
/// also be able to set its style") on its page under its Trend line, kept by a copy and the state file and repaired loudly;
/// a Detailed graph's card taller; and what a click on it opens (owner: "they should open the datasource when clicked /
/// page that shows that metric"), declared on every kind but those with no source.
/// </summary>
public sealed class MetricFitTests
{
    /// <summary>[S-14706] The kinds with no source to open: the sun's and the moon's times (their own widgets open nothing either).</summary>
    private static readonly HashSet<string> OpenNothing = new(StringComparer.Ordinal)
    {
        "sunrise", "sunset", "daylight", "solarNoon", "dawn", "dusk", "moonrise", "moonset", "nextFullMoon", "nextNewMoon",
    };

    private static IReadOnlyList<MetricModule> Metrics() => [.. ModuleRegistry.Widgets.OfType<MetricModule>()];

    private static WidgetMenuLines Menu() => new(() => { });

    [Fact]
    public void S14706_every_metric_kind_opens_its_source_or_is_one_with_none()
    {
        Assert.NotEmpty(Metrics());
        foreach (var module in Metrics())
        {
            var kind = module.Kind;
            Assert.Equal(!OpenNothing.Contains(kind.Id), kind.Opens is not null);
        }

        // Every name listed is a metric kind: the list never outlives the kinds it names.
        Assert.Subset(Metrics().Select(m => m.Kind.Id).ToHashSet(StringComparer.Ordinal), OpenNothing);
    }

    [Fact]
    public void S14706_a_system_metric_opens_what_its_family_s_meter_opens_and_a_place_s_metric_its_place_s_forecast()
    {
        var taskManager = System.IO.Path.Combine(Environment.SystemDirectory, "Taskmgr.exe");
        foreach (var module in Metrics().Where(m => m.Kind.Opens is not null))
        {
            var kind = module.Kind;
            var config = kind.Create();
            var target = kind.Opens!.Target(config)?.Value;
            var id = kind.Id;
            if (id.StartsWith("cpu", StringComparison.Ordinal) || id.StartsWith("gpu", StringComparison.Ordinal) || id.StartsWith("memory", StringComparison.Ordinal))
            {
                Assert.Equal(taskManager, target);
            }
            else if (id.StartsWith("network", StringComparison.Ordinal) || id.StartsWith("ping", StringComparison.Ordinal))
            {
                Assert.Equal("ms-settings:network-status", target);
            }
            else if (id.StartsWith("battery", StringComparison.Ordinal))
            {
                Assert.Equal("ms-settings:batterysaver", target);
            }
            else if (config is WeatherConfig weather)
            {
                // No place, no page; with one, the place's forecast, as the weather and air graphs open.
                Assert.Null(target);
                weather.Latitude = 47.3769;
                weather.Longitude = 8.5417;
                weather.LocationName = "Zurich, Switzerland";
                Assert.Contains("/weather/forecast/in-Zurich?loc=", kind.Opens.Target(config)!.Value, StringComparison.Ordinal);
            }
            else
            {
                // A drive's facts: the drive (the system drive while none is chosen), as the Drive widget opens it.
                Assert.IsAssignableFrom<IDriveChoice>(config);
                Assert.Equal(SystemDrive.Root, target);
            }

            Assert.False(string.IsNullOrWhiteSpace(kind.Opens.Label.Text()));
        }
    }

    [Fact]
    public void S14703_a_metric_with_a_series_offers_its_history_s_graphs_types_and_none_without()
    {
        foreach (var module in Metrics())
        {
            var config = module.Descriptor.CreateDefault();
            if (config is not IMetricTrend trend)
            {
                continue;
            }

            var looks = Assert.IsAssignableFrom<IChartLooks>(config);

            // Today's look unasked: Area and Simple.
            Assert.Equal((ChartType.Area, ChartDetail.Simple, ChartType.Area), (looks.Chart, looks.Detail, looks.DefaultChart));
            Assert.Empty(ChartLooks.Repair(looks, module.Kind.Id));
            if (trend.History != MetricHistory.Series)
            {
                Assert.Empty(looks.ChartTypes);
                Assert.False(looks.OffersDetail);
                continue;
            }

            // A place's hours draw as the weather graphs do (a column an hour too); the hub's series as the meters' histories.
            Assert.Equal(config is WeatherConfig ? ChartLooks.Hours : ChartLooks.Lines, looks.ChartTypes);
            Assert.True(looks.OffersDetail);
        }
    }

    [Fact]
    public void S14703_the_chart_look_is_on_the_page_under_the_trend_only_while_the_trend_is_the_graph()
    {
        var withSeries = Metrics().Where(m => m.Descriptor.CreateDefault() is IMetricTrend { History: MetricHistory.Series }).ToList();
        Assert.NotEmpty(withSeries);
        foreach (var module in withSeries)
        {
            // The family's source line stands before these (a place, a drive, an adapter: MetricModule.MenuLines).
            var config = (IMetricTrend)module.Descriptor.CreateDefault();
            var none = Menu();
            MetricMenuLines.Add(none, config);
            Assert.Equal([Str.Metric_Trend.Text()], none.Lines.Select(l => l.Text));

            config.Trend = MetricTrend.Arrow;
            var arrow = Menu();
            MetricMenuLines.Add(arrow, config);
            Assert.Single(arrow.Lines);

            config.Trend = MetricTrend.Line;
            var line = Menu();
            MetricMenuLines.Add(line, config);
            Assert.Equal([Str.Metric_Trend.Text(), Str.Chart_Type.Text(), Str.Chart_Detail.Text()], line.Lines.TakeLast(3).Select(l => l.Text));
            Assert.Equal([SharedSettings.ChartType.Key, SharedSettings.ChartDetail.Key], line.Lines.TakeLast(2).Select(l => l.Shares));
            Assert.Empty(line.StyleLines);
        }
    }

    [Fact]
    public void S14703_the_chart_look_survives_a_copy_and_the_state_file_and_a_detailed_graph_s_card_is_taller()
    {
        foreach (var module in Metrics())
        {
            var config = module.Descriptor.CreateDefault();
            if (config is not IMetricTrend { History: MetricHistory.Series } trend)
            {
                continue;
            }

            trend.Trend = MetricTrend.Line;
            var simple = module.Descriptor.Sizing(config);
            var looks = (IChartLooks)config;
            looks.Chart = ChartType.Line;
            looks.Detail = ChartDetail.Detailed;
            Assert.True(module.Descriptor.Sizing(config).Min.Height > simple.Min.Height, module.Kind.Id);

            var copy = (IChartLooks)config.Clone();
            Assert.Equal((ChartType.Line, ChartDetail.Detailed), (copy.Chart, copy.Detail));

            var options = new JsonSerializerOptions { TypeInfoResolver = module.Descriptor.Json };
            var json = JsonSerializer.Serialize(config, module.Descriptor.ConfigType, options);
            var back = (IChartLooks)JsonSerializer.Deserialize(json, module.Descriptor.ConfigType, options)!;
            Assert.Equal((ChartType.Line, ChartDetail.Detailed), (back.Chart, back.Detail));

            // Today's look is not written: a new widget's file is as it was.
            var plain = module.Descriptor.CreateDefault();
            Assert.DoesNotContain("\"chart\"", JsonSerializer.Serialize(plain, module.Descriptor.ConfigType, options), StringComparison.Ordinal);
        }
    }
}
