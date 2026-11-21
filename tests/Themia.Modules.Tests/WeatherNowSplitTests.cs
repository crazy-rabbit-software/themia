using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Themia.Core.Geometry;
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

    /// <summary>
    /// The every-kind fixture with widget <c>w00</c> a summary of <paramref name="config"/> at (0.2, 0.4), 0.3 × 0.15, or at
    /// <paramref name="rect"/>.
    /// </summary>
    private static ParseResult Load(string config, (double X, double Y, double W, double H)? rect = null)
    {
        var root = JsonNode.Parse(Fixture())!.AsObject();
        var widget = root["views"]![0]!["widgets"]!.AsArray().Single(w => (string?)w!["id"] == "w00")!;
        (widget["x"], widget["y"], widget["w"], widget["h"]) = rect ?? (0.2, 0.4, 0.3, 0.15);
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

    /// <summary>A display of 1920 × 1080 DIPs at 100 %.</summary>
    private static readonly ScreenMetrics Screen = new(1920, 1080);

    /// <summary>A summary of <paramref name="wDip"/> × <paramref name="hDip"/> DIPs at (0.2, 0.4), loaded and laid on <see cref="Screen"/>.</summary>
    private static (FracRect Card, List<WidgetInstance> Parts, List<string> Notes) LaidAt(double wDip, double hDip)
    {
        var card = new FracRect(0.2, 0.4, wDip / Screen.Width, hDip / Screen.Height);
        var result = Load(Placed, (card.X, card.Y, card.W, card.H));
        var state = result.State!;
        var view = state.Views[0];
        var shared = Assert.Single(result.SharedOut);
        Assert.Equal((view.Id, WeatherSummary.Removed), (shared.ViewId, shared.Kind));

        var outs = new ShareOuts();
        outs.Add(result.SharedOut);
        var notes = outs.Apply(state, [new SurfaceMetrics(view.Id, Screen, 1, Settled: true)]);
        Assert.False(outs.Waits([new SurfaceMetrics(view.Id, Screen, 1, Settled: true)]));
        return (card, [.. shared.Parts.Select(p => view.Find(p.Id)!)], notes);
    }

    /// <summary>Each part at least its minimum on <see cref="Screen"/>, inside the old card, none over another.</summary>
    private static void AssertHeld(FracRect card, List<WidgetInstance> parts)
    {
        const double eps = 1e-6;
        foreach (var part in parts)
        {
            var limits = SizeFit.Limits(part, Screen, 1)!;
            Assert.True(part.W >= limits.Width.Min - eps && part.H >= limits.Height.Min - eps, $"{part.Id} under its minimum");
            Assert.True(part.X >= card.X - eps && part.Y >= card.Y - eps && part.Rect.Right <= card.Right + eps && part.Rect.Bottom <= card.Bottom + eps,
                $"{part.Id} past the old card");
        }

        for (var i = 0; i < parts.Count; i++)
        {
            for (var j = i + 1; j < parts.Count; j++)
            {
                Assert.False(Lattice.Overlaps(parts[i].Rect, parts[j].Rect), $"{parts[i].Id} over {parts[j].Id}");
            }
        }
    }

    [Fact]
    public void S15026_a_wide_summary_keeps_its_parts_where_the_load_shared_them()
    {
        var (card, parts, notes) = LaidAt(576, 162);

        AssertHeld(card, parts);
        Assert.Empty(notes);
        Assert.Equal(ShareOuts.Placed(card, WeatherSummary.SkyShare), parts[0].Rect);
    }

    [Fact]
    public void S15026_a_narrow_summary_moves_the_line_beside_the_sky_so_the_temperature_keeps_its_minimum()
    {
        // 250 DIPs: two thirds is under the temperature's 200, the sky's least and the temperature's least are not.
        var (card, parts, notes) = LaidAt(250, 162);

        AssertHeld(card, parts);
        Assert.Single(notes);
        var (sky, temperature, condition) = (parts[0], parts[1], parts[2]);
        Assert.Equal(card.H, sky.H, 9);
        Assert.Equal(sky.Rect.Right, temperature.X, 9);
        Assert.Equal(temperature.X, condition.X, 9);
    }

    [Fact]
    public void S15026_a_summary_too_narrow_for_the_sky_beside_stacks_the_sky_above_the_temperature_and_the_condition()
    {
        // 212 DIPs: the temperature's least alone, no room for the sky beside it; 162 high holds the three one above the other.
        var (card, parts, _) = LaidAt(212, 162);

        AssertHeld(card, parts);
        var (sky, temperature, condition) = (parts[0], parts[1], parts[2]);
        Assert.All(parts, p => Assert.Equal(card.W, p.W, 9));
        Assert.True(sky.Rect.Bottom <= temperature.Y + 1e-9 && temperature.Rect.Bottom <= condition.Y + 1e-9);
    }

    [Fact]
    public void S15026_a_summary_too_small_for_either_grows_by_the_least_and_no_part_lies_over_another()
    {
        var (card, parts, _) = LaidAt(150, 60);

        foreach (var part in parts)
        {
            var limits = SizeFit.Limits(part, Screen, 1)!;
            Assert.True(part.W >= limits.Width.Min - 1e-6 && part.H >= limits.Height.Min - 1e-6);
            Assert.Equal((card.X, card.Y), (Math.Min(card.X, part.X), Math.Min(card.Y, part.Y)));
        }

        for (var i = 0; i < parts.Count; i++)
        {
            for (var j = i + 1; j < parts.Count; j++)
            {
                Assert.False(Lattice.Overlaps(parts[i].Rect, parts[j].Rect));
            }
        }
    }

    [Fact]
    public void S15026_parts_moved_before_their_display_settled_stay_where_the_user_put_them()
    {
        var card = new FracRect(0.2, 0.4, 212 / Screen.Width, 162 / Screen.Height);
        var result = Load(Placed, (card.X, card.Y, card.W, card.H));
        var view = result.State!.Views[0];
        var sky = view.Find(result.SharedOut[0].Parts[0].Id)!;
        sky.Rect = sky.Rect with { X = 0.6 };
        var before = view.Widgets.Select(w => w.Rect).ToList();

        var outs = new ShareOuts();
        outs.Add(result.SharedOut);
        Assert.False(outs.Waits([new SurfaceMetrics(view.Id, Screen, 1, Settled: false)]));
        Assert.Empty(outs.Apply(result.State, [new SurfaceMetrics(view.Id, Screen, 1, Settled: false)]));
        Assert.True(outs.Waits([new SurfaceMetrics(view.Id, Screen, 1, Settled: true)]));
        Assert.Empty(outs.Apply(result.State, [new SurfaceMetrics(view.Id, Screen, 1, Settled: true)]));
        Assert.Equal(before, view.Widgets.Select(w => w.Rect));
    }

    private static (double, double, double, double) Box(WidgetInstance w)
        => (Math.Round(w.X, 6), Math.Round(w.Y, 6), Math.Round(w.W, 6), Math.Round(w.H, 6));
}
