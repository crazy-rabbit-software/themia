using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-15022] The one kind-replacement pass over made-up kinds: a removed card one kind stands in for keeps its spot and id, one
/// several kinds share out becomes a copy each in its share of the rect, the old card's looks picking which.
/// </summary>
public sealed class KindMergesTests
{
    private sealed class Part : WidgetConfig
    {
        public override WidgetConfig Clone() => new Part();
    }

    private static WidgetDescriptor Kind(string id, params MergeFrom[] merges) => new()
    {
        Id = new(id),
        ConfigType = typeof(Part),
        CreateDefault = () => new Part(),
        Json = new DefaultJsonTypeInfoResolver(),
        Label = LocalText.Fixed(id),
        Description = LocalText.Fixed(id),
        Glyph = "",
        Sizing = _ => WidgetSizing.Fixed(new(64, 64)),
        Add = new(AddFolderId.More, 0, 0),
        Traits = WidgetTraits.None,
        Merges = merges,
    };

    /// <summary>The summary split: a sky and a condition the Minimal look never drew, the temperature always (alone when Minimal).</summary>
    private static readonly WidgetDescriptor[] Split =
    [
        Kind("sky", new MergeFrom("summary") { Unless = ("style", "minimal"), Share = new(0, 0, 0.25, 1) }),
        Kind("condition", new MergeFrom("summary") { Unless = ("style", "minimal"), Share = new(0.25, 0.5, 0.75, 0.5) }),
        Kind("temperature", new MergeFrom("summary") { When = ("style", "minimal") }, new MergeFrom("summary") { Share = new(0.25, 0, 0.75, 0.5) }),
    ];

    private static JsonObject Document(string config) => JsonNode.Parse($$"""
        { "views": [ { "name": "Main", "widgets": [
            { "id": "w01", "x": 0.2, "y": 0.4, "w": 0.4, "h": 0.2, "config": {{config}} },
            { "id": "w02", "x": 0, "y": 0, "w": 0.1, "h": 0.1, "config": { "kind": "other" } }
        ] } ] }
        """)!.AsObject();

    private static JsonArray Widgets(JsonObject document) => document["views"]![0]!["widgets"]!.AsArray();

    [Fact]
    public void S15022_a_card_several_kinds_share_out_becomes_a_copy_each_in_its_share_in_its_place()
    {
        var document = Document("""{ "kind": "summary", "style": "standard", "latitude": 47.4 }""");

        var notes = KindMerges.Replace(document, Split);

        var widgets = Widgets(document);
        Assert.Equal(["w01-sky", "w01-temperature", "w01-condition", "w02"], widgets.Select(w => (string)w!["id"]!));
        Assert.Equal(["sky", "temperature", "condition", "other"], widgets.Select(w => (string)w!["config"]!["kind"]!));

        static (double, double, double, double) Box(JsonNode? w)
            => (Math.Round((double)w!["x"]!, 6), Math.Round((double)w["y"]!, 6), Math.Round((double)w["w"]!, 6), Math.Round((double)w["h"]!, 6));
        Assert.Equal((0.2, 0.4, 0.1, 0.2), Box(widgets[0]));
        Assert.Equal((0.3, 0.4, 0.3, 0.1), Box(widgets[1]));
        Assert.Equal((0.3, 0.5, 0.3, 0.1), Box(widgets[2]));

        // Each keeps the card's settings; the key the card was shared out by goes.
        Assert.All(widgets.Take(3), w => Assert.Equal(47.4, (double)w!["config"]!["latitude"]!));
        Assert.All(widgets.Take(3), w => Assert.Null(w!["config"]!["style"]));
        Assert.Equal("replaced summary 'w01' in view 'Main' in place by 3 widgets in its spot: sky 'w01-sky', temperature 'w01-temperature', condition 'w01-condition'", Assert.Single(notes));
    }

    [Fact]
    public void S15022_a_look_that_drew_one_part_becomes_that_part_alone_in_the_whole_spot_with_its_id()
    {
        var document = Document("""{ "kind": "summary", "style": "Minimal" }""");

        var notes = KindMerges.Replace(document, Split);

        var widget = Widgets(document)[0]!;
        Assert.Equal(("w01", "temperature"), ((string)widget["id"]!, (string)widget["config"]!["kind"]!));
        Assert.Equal((0.2, 0.4, 0.4, 0.2), ((double)widget["x"]!, (double)widget["y"]!, (double)widget["w"]!, (double)widget["h"]!));
        Assert.Null(widget["config"]!["style"]);
        Assert.Null(widget["config"]!["trend"]);
        Assert.Equal(2, Widgets(document).Count);
        Assert.Equal("replaced summary 'w01' in view 'Main' in place by the widget temperature", Assert.Single(notes));
    }

    [Fact]
    public void S15022_a_card_without_a_stored_look_is_shared_out_by_the_rest()
    {
        var document = Document("""{ "kind": "summary" }""");

        KindMerges.Replace(document, Split);

        Assert.Equal(4, Widgets(document).Count);
    }

    [Fact]
    public void S15022_a_kind_that_is_still_in_the_catalog_is_never_replaced()
    {
        var document = Document("""{ "kind": "summary" }""");

        var notes = KindMerges.Replace(document, [.. Split, Kind("summary")]);

        Assert.Empty(notes);
        Assert.Equal("summary", (string)Widgets(document)[0]!["config"]!["kind"]!);
    }

    [Fact]
    public void S14713_a_graph_merge_shows_its_graph_and_its_window_is_the_span()
    {
        var document = Document("""{ "kind": "windGraph", "hours": 48 }""");

        KindMerges.Replace(document, [Kind("weatherWind", new MergeFrom("windGraph") { Graph = true })]);

        var config = Widgets(document)[0]!["config"]!;
        Assert.Equal(("weatherWind", "Line", 48), ((string)config["kind"]!, (string)config["trend"]!, (int)config["span"]!));
        Assert.Null(config["hours"]);
    }
}
