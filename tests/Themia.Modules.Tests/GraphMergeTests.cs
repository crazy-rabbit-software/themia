using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-14713] A placed widget of a removed graph kind (owner, 2026-11-21: "i think it would be good if we merge all the
/// different graph types into the general metric widget") is replaced in place on load by the single metric that stands in
/// for it, its graph shown, its settings kept: through the one kind-replacement path (<see cref="KindMerges"/>), before the
/// typed read.
/// </summary>
public sealed class GraphMergeTests
{
    private static string Fixture([CallerFilePath] string here = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "Fixtures", "state-every-kind.json"));

    /// <summary>The every-kind fixture with widget <c>w00</c> given <paramref name="config"/>.</summary>
    private static ParseResult Load(string config)
    {
        var root = JsonNode.Parse(Fixture())!.AsObject();
        var widgets = root["views"]![0]!["widgets"]!.AsArray();
        var widget = widgets.Single(w => (string?)w!["id"] == "w00")!;
        widget["x"] = 0.25;
        widget["y"] = 0.5;
        widget["config"] = JsonNode.Parse(config);
        return StateDocument.Parse(root.ToJsonString());
    }

    private static WidgetInstance Merged(ParseResult result)
    {
        Assert.Equal(ParseRefusal.None, result.Refusal);
        return Assert.Single(result.State!.Views[0].Widgets, w => w.Id == "w00");
    }

    [Fact]
    public void S14713_a_weather_graph_becomes_its_metric_in_its_spot_with_its_graph_its_place_its_look_and_its_window()
    {
        var result = Load("""{ "kind": "windGraph", "latitude": 47.37, "longitude": 8.54, "locationName": "Zurich", "hours": 48, "chart": "Area", "detail": "Detailed", "showGusts": false }""");
        var widget = Merged(result);

        Assert.Equal("weatherWind", widget.Kind.Value);
        Assert.Equal((0.25, 0.5), (widget.X, widget.Y));
        var config = Assert.IsAssignableFrom<IMetricGraph>(widget.Config);
        Assert.Equal(MetricTrend.Line, config.Trend);
        Assert.Equal((ChartType.Area, ChartDetail.Detailed, 48), (config.Chart, config.Detail, MetricGraphs.EffectiveSpan(config)));
        Assert.Equal((47.37, 8.54), (((WeatherConfig)widget.Config).Latitude, ((WeatherConfig)widget.Config).Longitude));

        // One line for the replacement; what the metric does not have (the gusts' line) is the load's to drop, loudly.
        Assert.Single(result.Repairs, r => r.StartsWith("replaced windGraph 'w00'", StringComparison.Ordinal));
        Assert.Contains(result.Repairs, r => r.Contains("showGusts", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("temperatureGraph", "weatherTemperature")]
    [InlineData("humidityGraph", "weatherHumidity")]
    [InlineData("pressureGraph", "weatherPressure")]
    [InlineData("uvGraph", "weatherUv")]
    [InlineData("rainGraph", "weatherPrecipitation")]
    [InlineData("cpuHistory", "cpuLoad")]
    [InlineData("memoryHistory", "memoryInUse")]
    [InlineData("gpuHistory", "gpuLoad")]
    [InlineData("diskHistory", "driveActivity")]
    [InlineData("networkHistory", "networkDownload")]
    [InlineData("batteryHistory", "batteryCharge")]
    [InlineData("pingHistory", "pingAverage")]
    [InlineData("airQualityGraph", "airIndex")]
    public void S14713_every_graph_kind_has_the_metric_that_stands_in_for_it(string graph, string metric)
    {
        var widget = Merged(Load($$"""{ "kind": "{{graph}}" }"""));

        Assert.Equal(metric, widget.Kind.Value);
        Assert.Equal(MetricTrend.Line, ((IMetricTrend)widget.Config).Trend);
        Assert.Contains(ModuleCatalog.Of(new WidgetId(metric)).Merges, m => m.Kind == graph);
    }

    [Fact]
    public void S14713_a_history_s_span_and_host_are_its_metric_s_and_the_air_graph_s_measure_picks_its_metric()
    {
        var ping = Merged(Load("""{ "kind": "pingHistory", "host": "example.org", "graphSpanSeconds": 1800, "chart": "Line" }"""));
        var config = (IMetricGraph)ping.Config;
        Assert.Equal(("pingAverage", 1800, ChartType.Line), (ping.Kind.Value, MetricGraphs.EffectiveSpan(config), config.Chart));
        Assert.Equal("example.org", ((IPingHost)ping.Config).Host);

        // The span a history had by default is its metric's default: stored as none.
        Assert.Equal(0, ((IMetricGraph)Merged(Load("""{ "kind": "cpuHistory", "graphSpanSeconds": 60 }""")).Config).Span);

        Assert.Equal("airPm10", Merged(Load("""{ "kind": "airQualityGraph", "measure": "Pm10", "hours": 96 }""")).Kind.Value);
        Assert.Equal("airOzone", Merged(Load("""{ "kind": "airQualityGraph", "measure": "ozone" }""")).Kind.Value);
        Assert.Equal("airIndex", Merged(Load("""{ "kind": "airQualityGraph", "measure": "NitrogenDioxide" }""")).Kind.Value);
        Assert.Equal(96, MetricGraphs.EffectiveSpan((IMetricGraph)Merged(Load("""{ "kind": "airQualityGraph", "measure": "Pm25", "hours": 96 }""")).Config));
    }

    [Fact]
    public void S14713_no_graph_kind_is_left_and_the_stock_s_and_the_rate_s_histories_stay()
    {
        string[] gone = ["temperatureGraph", "rainGraph", "humidityGraph", "pressureGraph", "windGraph", "uvGraph", "airQualityGraph",
            "cpuHistory", "memoryHistory", "gpuHistory", "diskHistory", "networkHistory", "batteryHistory", "pingHistory"];
        Assert.All(gone, id => Assert.False(ModuleCatalog.TryGet(new WidgetId(id), out _), id));
        Assert.True(ModuleCatalog.TryGet(new WidgetId("stockHistory"), out _));
        Assert.True(ModuleCatalog.TryGet(new WidgetId("exchangeHistory"), out _));

        // Every merge names a removed kind (a graph, or [S-15022] the weather summary its parts share out), once.
        var merges = ModuleCatalog.Widgets.SelectMany(w => w.Merges).ToList();
        Assert.All(merges, m => Assert.Contains(m.Kind, (string[])[.. gone, "weatherNow"]));
        Assert.Equal(merges.Count, merges.Distinct().Count());
        Assert.All(merges, m => Assert.Equal(gone.Contains(m.Kind), m.Graph));
    }
}
