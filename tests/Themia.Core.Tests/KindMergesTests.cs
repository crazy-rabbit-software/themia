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
/// [S-14713] [S-16230] The load's kind passes over made-up kinds: a removed graph one kind stands in for keeps its spot and id
/// with its graph shown; the parts of a widget that is back fold into it.
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

    private static JsonObject Document(string config) => JsonNode.Parse($$"""
        { "views": [ { "name": "Main", "widgets": [
            { "id": "w01", "x": 0.2, "y": 0.4, "w": 0.4, "h": 0.2, "config": {{config}} },
            { "id": "w02", "x": 0, "y": 0, "w": 0.1, "h": 0.1, "config": { "kind": "other" } }
        ] } ] }
        """)!.AsObject();

    private static JsonArray Widgets(JsonObject document) => document["views"]![0]!["widgets"]!.AsArray();

    [Fact]
    public void S14713_a_graph_merge_shows_its_graph_and_its_window_is_the_span()
    {
        var document = Document("""{ "kind": "windGraph", "hours": 48 }""");

        KindMerges.Replace(document, [Kind("weatherWind", new MergeFrom("windGraph"))]);

        var config = Widgets(document)[0]!["config"]!;
        Assert.Equal(("weatherWind", "Line", 48), ((string)config["kind"]!, (string)config["trend"]!, (int)config["span"]!));
        Assert.Null(config["hours"]);
    }

    /// <summary>[S-16230] A track that is back and the parts it was split into: texts and buttons fold into it where they touch.</summary>
    private static readonly WidgetDescriptor[] Back =
    [
        Kind("track", new MergeFrom("artist") { Fold = MergeFold.Touching }, new MergeFrom("button") { Fold = MergeFold.Touching }),
        Kind("progress", new MergeFrom("elapsed") { Fold = MergeFold.Touching }),
        Kind("month", new MergeFrom("monthHeader") { Fold = MergeFold.Under }),
        Kind("cover"),
    ];

    private static JsonObject Placed(params (string Id, string Kind, double X, double Y, double W, double H)[] widgets)
    {
        var placed = new JsonArray();
        foreach (var (id, kind, x, y, w, h) in widgets)
        {
            placed.Add(new JsonObject { ["id"] = id, ["x"] = x, ["y"] = y, ["w"] = w, ["h"] = h, ["config"] = new JsonObject { ["kind"] = kind, ["hideWhenIdle"] = "never" } });
        }

        return new JsonObject { ["views"] = new JsonArray(new JsonObject { ["name"] = "Main", ["widgets"] = placed }) };
    }

    private static (string, string, double, double, double, double) Row(JsonNode? w)
        => ((string)w!["id"]!, (string)w["config"]!["kind"]!, Math.Round((double)w["x"]!, 6), Math.Round((double)w["y"]!, 6), Math.Round((double)w["w"]!, 6), Math.Round((double)w["h"]!, 6));

    [Fact]
    public void S16230_touching_parts_fold_into_the_card_they_touch_at_the_union_of_their_boxes()
    {
        // The owner's desktop of 2026-11-22: a cover, the title over the artist, three buttons right of them; the
        // progress's bar and its length under all of them (touching the buttons, but a part of another widget).
        var document = Placed(
            ("c", "cover", 0.10, 0.20, 0.03, 0.05),
            ("t", "track", 0.13, 0.20, 0.10, 0.02),
            ("p", "progress", 0.10, 0.25, 0.15, 0.02),
            ("a", "artist", 0.13, 0.22, 0.10, 0.03),
            ("b1", "button", 0.23, 0.20, 0.01, 0.05),
            ("b2", "button", 0.24, 0.20, 0.01, 0.05),
            ("e", "elapsed", 0.25, 0.25, 0.02, 0.02),
            ("x", "track", 0.80, 0.80, 0.10, 0.02));

        var notes = KindMerges.Replace(document, Back);

        Assert.Equal(
        [
            ("c", "cover", 0.10, 0.20, 0.03, 0.05),
            ("t", "track", 0.13, 0.20, 0.12, 0.05),
            ("p", "progress", 0.10, 0.25, 0.17, 0.02),
            ("x", "track", 0.80, 0.80, 0.10, 0.02),
        ], Widgets(document).Select(Row));
        Assert.All(Widgets(document), w => Assert.Equal("never", (string)w!["config"]!["hideWhenIdle"]!));
        Assert.Equal(
        [
            "folded artist 'a', button 'b1', button 'b2' in view 'Main' into the widget track 't' over their boxes",
            "folded elapsed 'e' in view 'Main' into the widget progress 'p' over their boxes",
        ], notes);
    }

    [Fact]
    public void S16230_parts_with_no_card_of_their_kind_become_one_where_the_first_stood()
    {
        var document = Placed(("b1", "button", 0.5, 0.5, 0.01, 0.05), ("a", "artist", 0.4, 0.5, 0.1, 0.03), ("b2", "button", 0.7, 0.5, 0.01, 0.05));

        KindMerges.Replace(document, Back);

        // b1 and a touch: one track over both, at b1's place in the drawing order; b2 stands alone and becomes a track.
        Assert.Equal([("b1", "track", 0.4, 0.5, 0.11, 0.05), ("b2", "track", 0.7, 0.5, 0.01, 0.05)], Widgets(document).Select(Row));
    }

    [Fact]
    public void S16230_a_header_folds_into_every_card_right_under_it_and_alone_becomes_one()
    {
        var document = Placed(
            ("h", "monthHeader", 0.30, 0.03, 0.24, 0.03),
            ("m1", "month", 0.30, 0.06, 0.12, 0.15),
            ("m2", "month", 0.42, 0.06, 0.12, 0.15),
            ("m3", "month", 0.60, 0.06, 0.12, 0.15),
            ("h2", "monthHeader", 0.80, 0.50, 0.10, 0.03));

        var notes = KindMerges.Replace(document, Back);

        Assert.Equal(
        [
            ("m1", "month", 0.30, 0.03, 0.12, 0.18),
            ("m2", "month", 0.42, 0.03, 0.12, 0.18),
            ("m3", "month", 0.60, 0.06, 0.12, 0.15),
            ("h2", "month", 0.80, 0.50, 0.10, 0.03),
        ], Widgets(document).Select(Row));
        Assert.Equal(
        [
            "folded monthHeader 'h' in view 'Main' into the month under it: 'm1', 'm2'",
            "folded monthHeader 'h2' in view 'Main' into the widget month in its own box",
        ], notes);
    }

    [Fact]
    public void S16230_a_part_whose_kind_is_still_in_the_catalog_never_folds()
    {
        var document = Placed(("t", "track", 0.1, 0.1, 0.1, 0.02), ("a", "artist", 0.1, 0.12, 0.1, 0.02));

        var notes = KindMerges.Replace(document, [.. Back, Kind("artist")]);

        Assert.Empty(notes);
        Assert.Equal(2, Widgets(document).Count);
    }

    /// <summary>[S-16200] A summary that is back, its two removed parts and the single metric that stood with them.</summary>
    private static readonly WidgetDescriptor[] Summary =
    [
        Kind("summary", new MergeFrom("sky") { Fold = MergeFold.Touching }, new MergeFrom("condition") { Fold = MergeFold.Touching },
            new MergeFrom("temperature") { Fold = MergeFold.Joins }),
        Kind("temperature"),
        Kind("forecast"),
    ];

    [Fact]
    public void S16200_a_metric_that_stays_joins_the_parts_it_touches_and_the_first_part_keeps_its_id()
    {
        var document = Placed(
            ("t", "temperature", 0.15, 0.1, 0.1, 0.02),
            ("s", "sky", 0.1, 0.1, 0.05, 0.04),
            ("c", "condition", 0.15, 0.12, 0.1, 0.02),
            ("f", "forecast", 0.1, 0.14, 0.15, 0.1));

        var notes = KindMerges.Replace(document, Summary);

        Assert.Equal([("s", "summary", 0.1, 0.1, 0.15, 0.04), ("f", "forecast", 0.1, 0.14, 0.15, 0.1)], Widgets(document).Select(Row));
        Assert.Equal("folded sky 's', temperature 't', condition 'c' in view 'Main' into the widget summary 's' over their boxes", Assert.Single(notes));
    }

    [Fact]
    public void S16200_a_metric_that_stays_never_starts_a_fold_nor_links_two_and_alone_stays()
    {
        // A temperature alone; another touching it but no part; a third between two skies it does not join them by.
        var document = Placed(
            ("t1", "temperature", 0.5, 0.5, 0.1, 0.02),
            ("t2", "temperature", 0.5, 0.52, 0.1, 0.02),
            ("s1", "sky", 0.1, 0.1, 0.05, 0.04),
            ("t3", "temperature", 0.15, 0.1, 0.1, 0.04),
            ("s2", "sky", 0.25, 0.1, 0.05, 0.04));

        KindMerges.Replace(document, Summary);

        var rows = Widgets(document).Select(Row).ToList();
        Assert.Contains(("t1", "temperature", 0.5, 0.5, 0.1, 0.02), rows);
        Assert.Contains(("t2", "temperature", 0.5, 0.52, 0.1, 0.02), rows);

        // The temperature between the skies joins the first; the second sky is a summary of its own.
        Assert.Contains(("s1", "summary", 0.1, 0.1, 0.15, 0.04), rows);
        Assert.Contains(("s2", "summary", 0.25, 0.1, 0.05, 0.04), rows);
        Assert.Equal(4, rows.Count);
    }
}
