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

        var result = AppStateStore.Parse(json);

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
        var untouched = AppStateStore.Parse(Fixture()).State!.Views[0].Widgets.Where(w => w.Id is not ("w03" or "w40")).ToList();

        var kept = AppStateStore.Parse(json).State!.Views[0].Widgets;

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
        var result = AppStateStore.Parse(WithIds(("w05", "Not An Id")).Json);

        Assert.Equal(ParseRefusal.None, result.Refusal);
        Assert.DoesNotContain(result.State!.Views[0].Widgets, w => w.Id == "w05");
        Assert.Single(result.Repairs);
    }
}
