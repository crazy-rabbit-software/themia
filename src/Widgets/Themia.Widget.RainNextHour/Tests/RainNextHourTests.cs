using System.Text.Json.Nodes;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.RainNextHour.Tests;

/// <summary>[S-5126] [S-11140] The rain atom's one layout and its size, on the family's tokens.</summary>
public sealed class RainNextHourTests
{
    [Fact]
    public void S11140_the_sentence_its_caption_and_the_bars_at_one_height_220_to_420_wide()
    {
        var sizing = RainLayout.Sizing();

        Assert.Equal(12 + 28 + 20 + 8 + (48 + 4 + 16) + 12, sizing.Natural.Height);
        Assert.Equal(sizing.Natural.Height, sizing.Max.Height);
        Assert.Equal((160.0, 300.0, 420.0), (sizing.Min.Width, sizing.Natural.Width, sizing.Max.Width));
    }

    [Fact]
    public void S16451_the_words_scale_down_as_one_to_the_caption_floor_and_the_plot_keeps_its_least()
    {
        var sizing = RainLayout.Sizing();

        // At its own height the words are their own size; shorter, smaller as one, never under the caption's 9.
        Assert.Equal(1, RainLayout.TextScale(sizing.Natural.Height));
        Assert.Equal(RainLayout.LeastScale, RainLayout.TextScale(40));
        Assert.Equal(9, FamilyType.Body.FontSize * RainLayout.LeastScale, 6);
        var mid = RainLayout.TextScale(95);
        Assert.InRange(mid, RainLayout.LeastScale, 1);
        Assert.Equal(95 - RainLayout.PlotLeast, RainLayout.OverAt(mid), 6);

        // The least card holds the words at their least over the plot's least; its capitals stay on the inset.
        Assert.Equal(Math.Ceiling(RainLayout.OverAt(RainLayout.LeastScale) + RainLayout.PlotLeast), sizing.Min.Height);
        Assert.True(sizing.Min.Height < 100);
        Assert.Equal(FamilySpace.Inset, RainLayout.SentenceTopAt(0.7) + (LineInk.Of(FamilyType.Title).Over * 0.7), 6);
    }

    [Fact]
    public void S5126_only_rain_to_tell_of_draws_the_sentence_and_the_bars()
    {
        Assert.True(RainLayout.Warns(RainHeadline.StartsIn));
        Assert.True(RainLayout.Warns(RainHeadline.StopsIn));
        Assert.True(RainLayout.Warns(RainHeadline.Continues));
        Assert.False(RainLayout.Warns(RainHeadline.Dry));
        Assert.False(RainLayout.Warns(RainHeadline.Outdated));
    }

    [Fact]
    public void S16436_every_quarter_stands_in_its_track_a_dry_hour_too()
    {
        var ends = new ChartEnds("Now", "12:30");
        var dry = RainLayout.Chart([.. Enumerable.Repeat(0.0, RainRules.WindowSteps)], ends, 300);
        var wet = RainLayout.Chart([0, 0, 0.2, 0.5, 0.8, 0.5, 0.2, 0.1], ends, 300);

        // The default values in the graph (WR-10): a column a quarter at nothing, in its track, never a bare hairline.
        Assert.True(dry.Tracks && wet.Tracks);
        Assert.Equal(RainRules.WindowSteps, dry.Series.Count);
        Assert.All(dry.Series, sample => Assert.Equal(0, sample.Value));

        // The cells run across the plot edge to edge, a column on each one's middle.
        Assert.Equal(300.0 / RainRules.WindowSteps / 2, dry.Series[0].X, 6);
        Assert.Equal(300 - (300.0 / RainRules.WindowSteps / 2), dry.Series[^1].X, 6);
        Assert.Equal(ends, wet.Ends);
    }

    [Fact]
    public void S16437_the_sentence_stands_its_capitals_on_the_card_inset()
    {
        Assert.Equal(FamilySpace.Inset, RainLayout.SentenceTop + LineInk.Of(FamilyType.Title).Over, 6);
        Assert.True(RainLayout.SentenceTop < FamilySpace.Inset);
        Assert.Equal("--", RainLayout.NoAnswer);
    }

    [Fact]
    public void S16201_the_sentence_and_the_total_fold_into_the_graph_they_touch_over_their_boxes()
    {
        // The Rain group as the split placed it: the sentence over the graph over the total; a total elsewhere alone.
        var document = JsonNode.Parse("""
            { "views": [ { "name": "Home", "widgets": [
                { "id": "o", "x": 0.1, "y": 0.1, "w": 0.2, "h": 0.05, "config": { "kind": "rainOutlook", "latitude": 47.4, "longitude": 8.5 } },
                { "id": "g", "x": 0.1, "y": 0.15, "w": 0.2, "h": 0.08, "config": { "kind": "rainNextHour", "latitude": 47.4, "longitude": 8.5 } },
                { "id": "t", "x": 0.1, "y": 0.23, "w": 0.2, "h": 0.03, "config": { "kind": "rainTotal", "latitude": 47.4, "longitude": 8.5, "trend": "None" } },
                { "id": "lone", "x": 0.6, "y": 0.6, "w": 0.2, "h": 0.03, "config": { "kind": "rainTotal" } }
            ] } ] }
            """)!;

        var notes = KindMerges.Replace(document);

        var widgets = document["views"]![0]!["widgets"]!.AsArray();
        static (string, string, double, double, double, double) Row(JsonNode? w)
            => ((string)w!["id"]!, (string)w["config"]!["kind"]!, Math.Round((double)w["x"]!, 6), Math.Round((double)w["y"]!, 6), Math.Round((double)w["w"]!, 6), Math.Round((double)w["h"]!, 6));
        Assert.Equal([("g", "rainNextHour", 0.1, 0.1, 0.2, 0.16), ("lone", "rainNextHour", 0.6, 0.6, 0.2, 0.03)], widgets.Select(Row));
        Assert.Equal(2, notes.Count);
    }
}
