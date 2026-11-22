using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Themia.Core.Model;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-16200] The weather summary is one widget again (owner, 2026-11-22: "before 19th. but the single metric widgets stay"):
/// the parts it was split into, as the owner's Home placed them (the sky on the left, the temperature over the condition
/// beside it, a forecast under them), fold back into one summary on load, the temperature with them; a temperature no part
/// touches stays the single metric.
/// </summary>
public sealed class WeatherNowFoldTests
{
    private static string Fixture([CallerFilePath] string here = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "Fixtures", "state-every-kind.json"));

    private const string Place = """ "latitude": 47.3615, "longitude": 9.45356, "locationName": "Gais", "useDeviceLocation": false """;

    /// <summary>The every-kind fixture with <paramref name="added"/> placed on its view.</summary>
    private static ParseResult Load(params (string Id, string Kind, double X, double Y, double W, double H)[] added)
    {
        var root = JsonNode.Parse(Fixture())!.AsObject();
        var widgets = root["views"]![0]!["widgets"]!.AsArray();
        foreach (var (id, kind, x, y, w, h) in added)
        {
            widgets.Add(JsonNode.Parse($$"""{ "id": "{{id}}", "x": {{x}}, "y": {{y}}, "w": {{w}}, "h": {{h}}, "config": { "kind": "{{kind}}", {{Place}} } }"""));
        }

        return StateDocument.Parse(root.ToJsonString());
    }

    [Fact]
    public void S16200_the_owners_sky_temperature_and_condition_fold_into_one_summary_over_their_boxes()
    {
        var result = Load(
            ("f287-weatherSky", "weatherSky", 0.8516, 0.0069, 0.043, 0.0556),
            ("f287-weatherTemperature", "weatherTemperature", 0.8945, 0.0069, 0.082, 0.0278),
            ("f287-weatherCondition", "weatherCondition", 0.8945, 0.0347, 0.082, 0.0278),
            ("2fb5", "weatherForecast", 0.8516, 0.0625, 0.125, 0.2778));

        Assert.Equal(ParseRefusal.None, result.Refusal);
        var widgets = result.State!.Views[0].Widgets;
        Assert.DoesNotContain(widgets, w => w.Id.StartsWith("f287-weather", StringComparison.Ordinal) && w.Id != "f287-weatherSky");

        var summary = Assert.Single(widgets, w => w.Id == "f287-weatherSky");
        Assert.Equal("weatherNow", summary.Kind.Value);
        Assert.Equal(WeatherNowStyle.Standard, ((WeatherNowConfig)summary.Config).Style);
        Assert.Equal((47.3615, 9.45356), (((WeatherConfig)summary.Config).Latitude, ((WeatherConfig)summary.Config).Longitude));
        Assert.Equal((0.8516, 0.0069, 0.1249, 0.0556), (Math.Round(summary.Rect.X, 4), Math.Round(summary.Rect.Y, 4), Math.Round(summary.Rect.W, 4), Math.Round(summary.Rect.H, 4)));

        // The forecast under it is not a part: it stays as it was.
        Assert.Equal("weatherForecast", Assert.Single(widgets, w => w.Id == "2fb5").Kind.Value);
        Assert.Single(result.Repairs, r => r.StartsWith("folded weatherSky 'f287-weatherSky', weatherTemperature", StringComparison.Ordinal));
    }

    [Fact]
    public void S16200_a_temperature_no_part_touches_stays_the_single_metric()
    {
        var result = Load(("t", "weatherTemperature", 0.5, 0.5, 0.1, 0.03));

        Assert.Equal("weatherTemperature", Assert.Single(result.State!.Views[0].Widgets, w => w.Id == "t").Kind.Value);
        Assert.DoesNotContain(result.Repairs, r => r.StartsWith("folded", StringComparison.Ordinal));
    }

    [Fact]
    public void S16200_a_condition_alone_becomes_a_summary_in_its_own_box()
    {
        var result = Load(("c", "weatherCondition", 0.5, 0.5, 0.1, 0.03));

        var summary = Assert.Single(result.State!.Views[0].Widgets, w => w.Id == "c");
        Assert.Equal("weatherNow", summary.Kind.Value);
        Assert.Equal((0.5, 0.5), (summary.Rect.X, summary.Rect.Y));
    }
}
