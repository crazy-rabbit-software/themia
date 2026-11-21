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

    /// <summary>The owner's display: 2560 × 1440 DIPs (3840 × 2160 at 150 %), its grid 10 DIPs a cell.</summary>
    private static readonly ScreenMetrics Screen = new(2560, 1440);

    /// <summary>
    /// The every-kind fixture's one view holding <paramref name="placed"/> alone: each a kind (its config the fixture's, or
    /// <paramref name="summary"/> for <c>weatherNow</c>) at a box in DIPs on <see cref="Screen"/>; loaded, then every card the
    /// load shared out laid on <see cref="Screen"/> once its metrics settled.
    /// </summary>
    private static (ViewState View, IReadOnlyList<ShareOut> Shared, List<string> Notes) LaidOn(
        params (string Id, string Kind, double X, double Y, double W, double H)[] placed)
    {
        var root = JsonNode.Parse(Fixture())!.AsObject();
        var widgets = root["views"]![0]!["widgets"]!.AsArray();
        var template = widgets[0]!.DeepClone().AsObject();
        var configs = widgets.Select(w => w!["config"]!).GroupBy(c => (string)c["kind"]!).ToDictionary(g => g.Key, g => g.First());
        widgets.Clear();
        foreach (var (id, kind, x, y, w, h) in placed)
        {
            var widget = template.DeepClone().AsObject();
            (widget["id"], widget["x"], widget["y"], widget["w"], widget["h"]) = (id, x / Screen.Width, y / Screen.Height, w / Screen.Width, h / Screen.Height);
            widget["config"] = kind == WeatherSummary.Removed ? JsonNode.Parse(Placed) : configs[kind].DeepClone();
            widgets.Add(widget);
        }

        var result = StateDocument.Parse(root.ToJsonString());
        Assert.Equal(ParseRefusal.None, result.Refusal);
        var view = result.State!.Views[0];
        var outs = new ShareOuts();
        outs.Add(result.SharedOut);
        var settled = new SurfaceMetrics(view.Id, Screen, 1, Settled: true);
        var notes = outs.Apply(result.State, [settled]);
        Assert.False(outs.Waits([settled]));
        return (view, result.SharedOut, notes);
    }

    /// <summary>The box a card is painted in, in DIPs on <see cref="Screen"/>.</summary>
    private static (double X, double Y, double W, double H) Painted(WidgetInstance w)
    {
        var shown = SizeFit.Shown(w.Rect, SizeFit.Limits(w, Screen, 1), Screen);
        return (Math.Round(shown.X * Screen.Width, 6), Math.Round(shown.Y * Screen.Height, 6), Math.Round(shown.W * Screen.Width, 6), Math.Round(shown.H * Screen.Height, 6));
    }

    /// <summary>
    /// Every part painted on whole grid cells (whole tens of DIPs), at least its minimum, and no card painted over another;
    /// <paramref name="inside"/>, when given, holds every part.
    /// </summary>
    private static void AssertLaid(ViewState view, IEnumerable<string> partIds, (double X, double Y, double W, double H)? inside = null)
    {
        var parts = partIds.Select(id => view.Find(id)!).ToList();
        foreach (var part in parts)
        {
            var (x, y, w, h) = Painted(part);
            Assert.True(new[] { x, y, w, h }.All(v => Math.Abs(v - (10 * Math.Round(v / 10))) < 1e-6), $"{part.Id} off the grid: {x},{y} {w}x{h}");
            var limits = SizeFit.Limits(part, Screen, 1)!;
            Assert.True(part.W >= limits.Width.Min - 1e-9 && part.H >= limits.Height.Min - 1e-9, $"{part.Id} under its minimum");
            if (inside is var (cx, cy, cw, ch))
            {
                Assert.True(x >= cx && y >= cy && x + w <= cx + cw && y + h <= cy + ch, $"{part.Id} past the old card: {x},{y} {w}x{h}");
            }
        }

        var painted = view.Widgets.Select(w => (w.Id, Box: Painted(w))).ToList();
        foreach (var part in parts)
        {
            var (x, y, w, h) = Painted(part);
            foreach (var (id, (ox, oy, ow, oh)) in painted.Where(o => o.Id != part.Id))
            {
                var over = Math.Min(x + w, ox + ow) - Math.Max(x, ox) > 1e-6 && Math.Min(y + h, oy + oh) - Math.Max(y, oy) > 1e-6;
                Assert.False(over, $"{part.Id} at {x},{y} {w}x{h} over {id} at {ox},{oy} {ow}x{oh}");
            }
        }
    }

    private static string[] PartsOf(string id) => [$"{id}-weatherSky", $"{id}-weatherTemperature", $"{id}-weatherCondition"];

    /// <summary>
    /// The owner's Home view round its two summaries (2026-11-21): one 300 × 110 over a forecast, the quick toggles at 30 on
    /// its left above it; one 320 × 80 over a forecast, the quick toggles at 60 on its left ending at 2170.
    /// </summary>
    private static (ViewState View, IReadOnlyList<ShareOut> Shared, List<string> Notes) OwnersHome() => LaidOn(
        ("narrow", WeatherSummary.Removed, 1870, 80, 300, 110),
        ("narrowForecast", "weatherForecast", 1870, 190, 300, 120),
        ("t1", "doNotDisturbToggle", 1870, 10, 30, 30),
        ("t2", "nightLightToggle", 1900, 10, 30, 30),
        ("t3", "wifiToggle", 1870, 40, 30, 30),
        ("t4", "bluetoothToggle", 1900, 40, 30, 30),
        ("wide", WeatherSummary.Removed, 2180, 10, 320, 80),
        ("wideForecast", "weatherForecast", 2180, 90, 320, 400),
        ("t5", "wifiToggle", 1930, 10, 60, 60),
        ("t6", "bluetoothToggle", 1990, 10, 60, 60),
        ("t7", "doNotDisturbToggle", 2050, 10, 60, 60),
        ("t8", "nightLightToggle", 2110, 10, 60, 60));

    [Fact]
    public void S15027_the_owners_summary_over_a_forecast_keeps_its_parts_on_the_grid_in_its_card()
    {
        // Laid in their shares the condition stood at 135.3, painted 60 tall in a card 110 tall: over the temperature
        // and 5.3 over the forecast under it.
        var (view, _, _) = OwnersHome();

        AssertLaid(view, PartsOf("narrow"), inside: (1870, 80, 300, 110));
        Assert.Equal((1870, 80, 100, 110), Painted(view.Find("narrow-weatherSky")!));
        Assert.Equal((1970, 80, 200, 60), Painted(view.Find("narrow-weatherTemperature")!));
        Assert.Equal((1970, 140, 200, 50), Painted(view.Find("narrow-weatherCondition")!));
    }

    [Fact]
    public void S15027_the_owners_summary_beside_the_toggles_keeps_its_parts_on_the_grid_in_its_card()
    {
        // Laid in their shares the temperature and the condition stood at 2287.3, painted 220 wide: over the sky and past
        // the forecast's right edge at 2500.
        var (view, _, _) = OwnersHome();

        AssertLaid(view, PartsOf("wide"), inside: (2180, 10, 320, 80));
        Assert.Equal((2180, 10, 110, 80), Painted(view.Find("wide-weatherSky")!));
        Assert.Equal((2290, 10, 210, 40), Painted(view.Find("wide-weatherTemperature")!));
        Assert.Equal((2290, 50, 210, 40), Painted(view.Find("wide-weatherCondition")!));
    }

    [Fact]
    public void S15026_a_wide_summary_keeps_its_parts_where_the_load_shared_them()
    {
        var (view, shared, notes) = LaidOn(("w", WeatherSummary.Removed, 600, 300, 600, 160));

        AssertLaid(view, PartsOf("w"), inside: (600, 300, 600, 160));
        Assert.Empty(notes);
        Assert.Equal(ShareOuts.Placed(shared[0].Card, WeatherSummary.SkyShare), view.Find("w-weatherSky")!.Rect);
    }

    [Fact]
    public void S15026_a_narrow_summary_moves_the_line_beside_the_sky_so_the_temperature_keeps_its_minimum()
    {
        // 250 DIPs: two thirds is under the temperature's 200, the sky's least and the temperature's least are not.
        var (view, _, notes) = LaidOn(("w", WeatherSummary.Removed, 600, 300, 250, 160));

        AssertLaid(view, PartsOf("w"), inside: (600, 300, 250, 160));
        Assert.Single(notes);
        Assert.Equal((600, 300, 50, 160), Painted(view.Find("w-weatherSky")!));
        Assert.Equal((650, 300, 200, 80), Painted(view.Find("w-weatherTemperature")!));
    }

    [Fact]
    public void S15026_a_summary_too_narrow_for_the_sky_beside_stacks_the_sky_above_the_temperature_and_the_condition()
    {
        // 210 DIPs: the temperature's least alone, no room for the sky beside it; 160 high holds the three one above the other.
        var (view, _, _) = LaidOn(("w", WeatherSummary.Removed, 600, 300, 210, 160));

        AssertLaid(view, PartsOf("w"), inside: (600, 300, 210, 160));
        var (sky, temperature, condition) = (Painted(view.Find("w-weatherSky")!), Painted(view.Find("w-weatherTemperature")!), Painted(view.Find("w-weatherCondition")!));
        Assert.All(new[] { sky, temperature, condition }, p => Assert.Equal(210, p.W));
        Assert.True(sky.Y + sky.H <= temperature.Y && temperature.Y + temperature.H <= condition.Y);
    }

    [Fact]
    public void S15027_a_summary_too_small_for_either_grows_toward_free_cells_never_over_a_neighbour()
    {
        // 150 × 60 with a card hard under it and one hard on its right: the block grows up and left, where the cells are free.
        var (view, _, _) = LaidOn(
            ("w", WeatherSummary.Removed, 600, 300, 150, 60),
            ("below", "weatherForecast", 600, 360, 300, 120),
            ("right", "wifiToggle", 750, 300, 60, 60));

        AssertLaid(view, PartsOf("w"));
    }

    [Fact]
    public void S15027_a_summary_with_no_free_cells_round_it_moves_to_the_free_cells_nearest_it()
    {
        // Boxed in on every side: the parts go to the nearest free cells, never over a neighbour.
        var (view, _, _) = LaidOn(
            ("w", WeatherSummary.Removed, 600, 300, 150, 60),
            ("below", "weatherForecast", 500, 360, 400, 120),
            ("above", "weatherForecast", 500, 180, 400, 120),
            ("left", "wifiToggle", 540, 300, 60, 60),
            ("right", "bluetoothToggle", 750, 300, 60, 60));

        AssertLaid(view, PartsOf("w"));
    }

    [Fact]
    public void S15026_parts_moved_before_their_display_settled_stay_where_the_user_put_them()
    {
        var root = JsonNode.Parse(Fixture())!.AsObject();
        var widget = root["views"]![0]!["widgets"]!.AsArray().Single(w => (string?)w!["id"] == "w00")!;
        (widget["x"], widget["y"], widget["w"], widget["h"]) = (600 / Screen.Width, 300 / Screen.Height, 210 / Screen.Width, 160 / Screen.Height);
        widget["config"] = JsonNode.Parse(Placed);
        var result = StateDocument.Parse(root.ToJsonString());
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
