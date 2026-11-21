using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-15025] A placed weather summary (<c>weatherNow</c>, gone: owner, 2026-11-21, "weather summary should also be split into
/// three widgets") becomes its three parts in its spot on load, through the one kind-replacement pass, its place kept by
/// each; its Minimal look (the temperature alone) becomes the temperature alone.
/// </summary>
public sealed class WeatherNowSplitTests
{
    private static string Fixture([CallerFilePath] string here = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "Fixtures", "state-every-kind.json"));

    /// <summary>The every-kind fixture with widget <c>w00</c> a summary of <paramref name="config"/> at (0.2, 0.4), 0.3 × 0.15.</summary>
    private static ParseResult Load(string config)
    {
        var root = JsonNode.Parse(Fixture())!.AsObject();
        var widget = root["views"]![0]!["widgets"]!.AsArray().Single(w => (string?)w!["id"] == "w00")!;
        (widget["x"], widget["y"], widget["w"], widget["h"]) = (0.2, 0.4, 0.3, 0.15);
        widget["config"] = JsonNode.Parse(config);
        return StateDocument.Parse(root.ToJsonString());
    }

    private const string Placed = """{ "kind": "weatherNow", "style": "largeIcon", "latitude": 47.37, "longitude": 8.54, "locationName": "Zurich" }""";

    [Fact]
    public void S15025_a_placed_summary_becomes_the_sky_the_temperature_and_the_condition_in_its_spot_each_at_its_place()
    {
        var result = Load(Placed);

        Assert.Equal(ParseRefusal.None, result.Refusal);
        var widgets = result.State!.Views[0].Widgets;
        var at = widgets.FindIndex(w => w.Id == "w00-weatherSky");
        Assert.Equal(["w00-weatherSky", "w00-weatherTemperature", "w00-weatherCondition"], widgets.Skip(at).Take(3).Select(w => w.Id));
        Assert.Equal(["weatherSky", "weatherTemperature", "weatherCondition"], widgets.Skip(at).Take(3).Select(w => w.Kind.Value));
        Assert.DoesNotContain(widgets, w => w.Id == "w00");

        var (sky, temperature, condition) = (widgets[at], widgets[at + 1], widgets[at + 2]);
        Assert.Equal((0.2, 0.4, 0.1, 0.15), Box(sky));
        Assert.Equal((0.3, 0.4, 0.2, 0.075), Box(temperature));
        Assert.Equal((0.3, 0.475, 0.2, 0.075), Box(condition));
        Assert.All([sky, temperature, condition], w => Assert.Equal((47.37, 8.54), (((WeatherConfig)w.Config).Latitude, ((WeatherConfig)w.Config).Longitude)));

        // The temperature is the plain metric (no graph): its trend stays None.
        Assert.Equal(MetricTrend.None, ((IMetricTrend)temperature.Config).Trend);
        Assert.Single(result.Repairs, r => r.StartsWith("replaced weatherNow 'w00' in view", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Repairs, r => r.Contains("style", StringComparison.Ordinal));
    }

    [Fact]
    public void S15025_a_minimal_summary_was_the_temperature_alone_and_becomes_it_in_its_whole_spot()
    {
        var result = Load("""{ "kind": "weatherNow", "style": "minimal", "latitude": 47.37, "longitude": 8.54 }""");

        var widget = Assert.Single(result.State!.Views[0].Widgets, w => w.Id == "w00");
        Assert.Equal("weatherTemperature", widget.Kind.Value);
        Assert.Equal((0.2, 0.4, 0.3, 0.15), Box(widget));
        Assert.DoesNotContain(result.State.Views[0].Widgets, w => w.Id.StartsWith("w00-", StringComparison.Ordinal));
    }

    [Fact]
    public void S15025_no_group_first_run_seed_or_bento_names_the_summary_any_more()
    {
        Assert.DoesNotContain(ReadyMadeGroups.All.SelectMany(g => g.Widgets), w => w.Kind == WeatherSummary.Removed);
        Assert.DoesNotContain(FirstRunDesktop.Home, s => s.Id == WeatherSummary.Removed);
        Assert.Contains(ReadyMadeGroups.All, g => g.Id == "weatherSummary"
            && g.Widgets.Select(w => w.Kind).SequenceEqual(["header", "weatherSky", "weatherTemperature", "weatherCondition"]));
    }

    private static (double, double, double, double) Box(WidgetInstance w)
        => (Math.Round(w.X, 6), Math.Round(w.Y, 6), Math.Round(w.W, 6), Math.Round(w.H, 6));
}
