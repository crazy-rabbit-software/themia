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
    public void S14703_S14710_a_metric_with_a_series_offers_its_kind_s_graph_s_types_and_none_without()
    {
        foreach (var module in Metrics())
        {
            var config = module.Descriptor.CreateDefault();
            if (config is not IMetricTrend trend)
            {
                continue;
            }

            var graph = Assert.IsAssignableFrom<IMetricGraph>(config);

            // [S-14710] Every kind with a series declares its graph, every other none; the config takes it from the kind.
            Assert.Equal(trend.History == MetricHistory.Series, module.Kind.Graph is not null);
            Assert.Same(module.Kind.Graph, graph.Graph);

            // Its kind's look unasked (the look its merged graph had, [S-6752]) and Simple; nothing to repair.
            Assert.Equal((MetricGraphs.Default(module.Kind.Graph), ChartDetail.Simple), (graph.Chart, graph.Detail));
            Assert.Empty(ChartLooks.Repair(graph, module.Kind.Id));
            Assert.Null(MetricGraphs.RepairSpan(graph, module.Kind.Id));
            if (module.Kind.Graph is not { } spec)
            {
                Assert.Empty(graph.ChartTypes);
                Assert.False(graph.OffersDetail);
                continue;
            }

            // A place's hours draw as the weather graphs do (a column an hour too); the hub's series as the meters' histories.
            Assert.Equal(config is WeatherConfig ? GraphSeries.Hours : GraphSeries.Hub, spec.Series);
            Assert.Equal(config is WeatherConfig ? ChartLooks.Hours : ChartLooks.Lines, graph.ChartTypes);
            Assert.Contains(spec.Default, spec.Types);
            Assert.Contains(spec.Span, spec.Spans);
            Assert.True(graph.OffersDetail);
        }

        // [S-6752] The looks the merged graphs had stay their metrics' defaults: the wind's and the pressure's a line, the
        // rain's and the UV's columns, the rest the area.
        Assert.Equal(ChartType.Line, Default("weatherWind"));
        Assert.Equal(ChartType.Line, Default("weatherPressure"));
        Assert.Equal(ChartType.Bars, Default("weatherPrecipitation"));
        Assert.Equal(ChartType.Bars, Default("weatherUv"));
        Assert.Equal(ChartType.Area, Default("weatherTemperature"));
        Assert.Equal(ChartType.Area, Default("cpuLoad"));
    }

    private static ChartType Default(string id) => ((IChartLooks)Metrics().Single(m => m.Kind.Id == id).Descriptor.CreateDefault()).Chart;

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
            // [S-14711] Then its span, where its graph offers more than one.
            Assert.Equal([Str.Metric_Trend.Text(), Str.Chart_Type.Text(), Str.Chart_Detail.Text()], line.Lines.Skip(line.Lines.Count - 4).Take(3).Select(l => l.Text));
            Assert.Equal([SharedSettings.ChartType.Key, SharedSettings.ChartDetail.Key, SharedSettings.MetricSpan.Key], line.Lines.TakeLast(3).Select(l => l.Shares));
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
            var looks = (IMetricGraph)config;

            // A type other than its default, so it is stored; a span other than its default too.
            var other = looks.ChartTypes.First(t => t != looks.DefaultChart);
            var span = looks.Graph!.Spans.First(s => s != looks.Graph.Span);
            looks.Chart = other;
            looks.Detail = ChartDetail.Detailed;
            looks.Span = span;

            // [S-14712] Detailed asks more of a new card; the least is still one cell (the graph alone).
            Assert.True(module.Descriptor.Sizing(config).Natural.Height > simple.Natural.Height, module.Kind.Id);
            Assert.Equal(simple.Min.Height, module.Descriptor.Sizing(config).Min.Height);

            var copy = (IMetricGraph)config.Clone();
            Assert.Equal((other, ChartDetail.Detailed, span), (copy.Chart, copy.Detail, copy.Span));

            var options = new JsonSerializerOptions { TypeInfoResolver = module.Descriptor.Json };
            var json = JsonSerializer.Serialize(config, module.Descriptor.ConfigType, options);
            var back = (IMetricGraph)JsonSerializer.Deserialize(json, module.Descriptor.ConfigType, options)!;
            Assert.Equal((other, ChartDetail.Detailed, span), (back.Chart, back.Detail, back.Span));

            // Today's look is not written: a new widget's file is as it was.
            var plain = module.Descriptor.CreateDefault();
            Assert.DoesNotContain("\"chart\"", JsonSerializer.Serialize(plain, module.Descriptor.ConfigType, options), StringComparison.Ordinal);
        }
    }
}
