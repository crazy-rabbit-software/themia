using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-6908] A widget whose id no module in this build declares (a file from a build with a module
/// this one lacks, a hand edit) is dropped on load with a logged notice; the rest of the document
/// loads. System.Text.Json refuses an unknown discriminator for the whole document, so the store
/// takes such a widget out element by element before the typed read.
/// </summary>
public sealed class UnknownWidgetTests
{
    private static string Fixture([CallerFilePath] string here = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "Fixtures", "state-every-kind.json"));

    /// <summary>The every-kind fixture with the config ids of some widgets replaced.</summary>
    private static (string Json, int Count) WithIds(params (string WidgetId, string ConfigId)[] changes)
    {
        var root = JsonNode.Parse(Fixture())!.AsObject();
        var widgets = root["views"]![0]!["widgets"]!.AsArray();
        foreach (var (widget, id) in changes)
        {
            widgets.Single(w => (string?)w!["id"] == widget)!["config"]!["kind"] = id;
        }

        return (root.ToJsonString(), widgets.Count);
    }

    [Fact]
    public void S6908_a_widget_of_an_id_this_build_lacks_is_dropped_and_named_with_its_view()
    {
        var (json, count) = WithIds(("w00", "hello"));

        var result = StateDocument.Parse(json);

        Assert.Equal(ParseRefusal.None, result.Refusal);
        var widgets = Assert.Single(result.State!.Views).Widgets;
        Assert.Equal(count - 1, widgets.Count);
        Assert.DoesNotContain(widgets, w => w.Id == "w00");
        var repair = Assert.Single(result.Repairs);
        Assert.Contains("'w00'", repair, StringComparison.Ordinal);
        Assert.Contains("'hello'", repair, StringComparison.Ordinal);
        Assert.Contains("'Every kind'", repair, StringComparison.Ordinal);
    }

    [Fact]
    public void S6908_every_other_widget_is_kept_as_it_was()
    {
        var (json, count) = WithIds(("w03", "hello"), ("w40", "nope"));
        var untouched = StateDocument.Parse(Fixture()).State!.Views[0].Widgets.Where(w => w.Id is not ("w03" or "w40")).ToList();

        var kept = StateDocument.Parse(json).State!.Views[0].Widgets;

        Assert.Equal(count - 2, kept.Count);
        Assert.Equal(untouched.Select(w => (w.Id, w.Kind)), kept.Select(w => (w.Id, w.Kind)));
    }

    [Fact]
    public void S6908_a_file_with_an_unknown_id_loads_and_says_so_rather_than_being_set_aside()
    {
        var path = Path.Combine(Path.GetTempPath(), $"themia-unknown-{Guid.NewGuid():n}.json");
        try
        {
            File.WriteAllText(path, WithIds(("w10", "hello")).Json);

            var result = new AppStateStore(path).Load();

            Assert.Equal(LoadOutcome.Loaded, result.Outcome);
            Assert.Contains("'hello'", result.Detail, StringComparison.Ordinal);
            Assert.True(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void S6908_a_malformed_id_is_dropped_like_an_unknown_one()
    {
        var result = StateDocument.Parse(WithIds(("w05", "Not An Id")).Json);

        Assert.Equal(ParseRefusal.None, result.Refusal);
        Assert.DoesNotContain(result.State!.Views[0].Widgets, w => w.Id == "w05");
        Assert.Single(result.Repairs);
    }

    [Fact]
    public void S13900_placed_weather_facts_widgets_are_dropped_on_load_with_their_count_and_nothing_else_is()
    {
        // The weather facts kind was split into a widget a fact (owner, 2026-11-19): a placed one is removed, not converted.
        var (json, count) = WithIds(("w03", "weatherFacts"), ("w07", "weatherFacts"));

        var result = StateDocument.Parse(json);

        Assert.Equal(ParseRefusal.None, result.Refusal);
        var widgets = Assert.Single(result.State!.Views).Widgets;
        Assert.Equal(count - 2, widgets.Count);
        Assert.DoesNotContain(widgets, w => w.Id is "w03" or "w07");
        var repair = Assert.Single(result.Repairs);
        Assert.StartsWith("dropped 2 widgets of unknown kind 'weatherFacts'", repair, StringComparison.Ordinal);
        Assert.Contains("'w03'", repair, StringComparison.Ordinal);
        Assert.Contains("'w07'", repair, StringComparison.Ordinal);
    }

    [Fact]
    public void S14606_a_split_kind_with_no_pick_stored_becomes_its_defaults_and_a_sky_card_its_times_with_its_place()
    {
        var root = JsonNode.Parse(Fixture())!.AsObject();
        var widgets = root["views"]![0]!["widgets"]!.AsArray();
        var count = widgets.Count;
        widgets.Single(w => (string?)w!["id"] == "w03")!["config"] = JsonNode.Parse("""{ "kind": "airFacts", "latitude": 47.37, "longitude": 8.54, "locationName": "Zurich" }""");
        widgets.Single(w => (string?)w!["id"] == "w07")!["config"] = JsonNode.Parse("""{ "kind": "sunTimes", "times": ["Dusk", "nope", "Dusk"] }""");

        var result = StateDocument.Parse(root.ToJsonString());

        Assert.Equal(ParseRefusal.None, result.Refusal);
        var placed = Assert.Single(result.State!.Views).Widgets;
        Assert.Equal(count - 2 + 4 + 1, placed.Count);
        Assert.Equal(["w03-airUv", "w03-airPm25", "w03-airPollen", "w03-airPm10"], placed.Where(w => w.Id.StartsWith("w03", StringComparison.Ordinal)).Select(w => w.Id));
        Assert.All(placed.Where(w => w.Id.StartsWith("w03-", StringComparison.Ordinal)), w => Assert.Equal(47.37, ((Themia.Core.Model.WeatherConfig)w.Config).Latitude));
        Assert.Equal("w07-dusk", Assert.Single(placed, w => w.Id.StartsWith("w07", StringComparison.Ordinal)).Id);
        Assert.Equal(2, result.Repairs.Count(r => r.StartsWith("replaced ", StringComparison.Ordinal)));
    }
}
