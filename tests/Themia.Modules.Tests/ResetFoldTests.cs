using System.Text.Json.Nodes;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-16230] The owner's desktop of 2026-11-22, its split parts placed as he had them (in pixels of a 1920 × 1080 display):
/// the now-playing texts and buttons fold into the track, the length into the progress, the calendar header into the two
/// calendars under it, through the real kinds' declarations.
/// </summary>
public sealed class ResetFoldTests
{
    private static JsonObject Desktop(params (string Id, string Kind, double Left, double Top, double Right, double Bottom)[] widgets)
    {
        var placed = new JsonArray();
        foreach (var (id, kind, left, top, right, bottom) in widgets)
        {
            placed.Add(new JsonObject
            {
                ["id"] = id,
                ["x"] = left / 1920,
                ["y"] = top / 1080,
                ["w"] = (right - left) / 1920,
                ["h"] = (bottom - top) / 1080,
                ["config"] = new JsonObject { ["kind"] = kind },
            });
        }

        return new JsonObject { ["views"] = new JsonArray(new JsonObject { ["name"] = "Home", ["widgets"] = placed }) };
    }

    private static List<(string Id, string Kind, double Left, double Top, double Right, double Bottom)> Placed(JsonObject document)
        => [.. document["views"]![0]!["widgets"]!.AsArray().Select(w => ((string)w!["id"]!, (string)w["config"]!["kind"]!,
            Math.Round((double)w["x"]! * 1920, 3), Math.Round((double)w["y"]! * 1080, 3),
            Math.Round(((double)w["x"]! + (double)w["w"]!) * 1920, 3), Math.Round(((double)w["y"]! + (double)w["h"]!) * 1080, 3)))];

    [Fact]
    public void S16230_the_owners_now_playing_and_calendar_parts_fold_back_in_place()
    {
        var document = Desktop(
            ("cal", "calendar", 30, 90, 292.5, 270),
            ("cover", "musicCover", 337.5, 236.25, 393.75, 285),
            ("title", "music", 393.75, 240, 637.5, 262.5),
            ("bar", "musicProgress", 337.5, 285, 660, 311.25),
            ("artist", "musicArtist", 393.75, 262.5, 637.5, 285),
            ("prev", "musicPrevious", 637.5, 240, 667.5, 285),
            ("play", "musicPlayPause", 667.5, 240, 705, 285),
            ("next", "musicNext", 705, 240, 735, 285),
            ("head", "calendarHeader", 562.5, 37.5, 1012.5, 67.5),
            ("month", "calendar", 562.5, 67.5, 787.5, 225),
            ("agenda", "calendar", 787.5, 67.5, 1012.5, 225),
            ("length", "musicDuration", 660, 285, 735, 311.25));

        var notes = KindMerges.Replace(document);

        // [S-16513] Now playing is one widget: the cover, the track's texts and buttons, the bar and its length touch one
        // another and become the track's card over all their boxes, showing every element they showed.
        Assert.Equal(
        [
            ("cal", "calendar", 30, 90, 292.5, 270),
            ("title", "music", 337.5, 236.25, 735, 311.25),
            ("month", "calendar", 562.5, 37.5, 787.5, 225),
            ("agenda", "calendar", 787.5, 37.5, 1012.5, 225),
        ], Placed(document));
        Assert.Equal(2, notes.Count);
        var music = document["views"]![0]!["widgets"]![1]!["config"]!;
        Assert.Equal(["cover", "title", "artist", "album", "elapsed", "bar", "length", "previous", "playPause", "next"],
            music["shows"]!.AsArray().Select(v => (string)v!).Order(Comparer<string>.Create((a, b) => Rank(a) - Rank(b))));
    }

    /// <summary>An element's place in the canonical order, by its wire name.</summary>
    private static int Rank(string element) => Enum.TryParse<Themia.Core.Model.MusicElement>(element, ignoreCase: true, out var e) ? (int)e : int.MaxValue;

    [Fact]
    public void S16513_the_owners_now_playing_of_2026_11_23_folds_into_one_widget_showing_all_three_parts()
    {
        // His desktop on 2026-11-23: the cover, the track 3.75 px right of it (not touching), the progress under both.
        var document = Desktop(
            ("cover", "musicCover", 337.5, 236.25, 393.75, 285),
            ("track", "music", 397.5, 240, 742.5, 285),
            ("bar", "musicProgress", 337.5, 285, 735, 311.25));

        var notes = KindMerges.Replace(document);

        Assert.Equal([("track", "music", 337.5, 236.25, 742.5, 311.25)], Placed(document));
        Assert.Single(notes);
        var config = Config(document["views"]![0]!["widgets"]![0]!);
        Assert.Empty(config.Repair());
        Assert.Equal(Themia.Core.Model.MusicElements.All, config.Shows);
    }

    [Fact]
    public void S16513_a_lone_cover_or_progress_becomes_now_playing_showing_just_what_it_showed()
    {
        var document = Desktop(("cover", "musicCover", 100, 100, 260, 260), ("bar", "musicProgress", 600, 600, 940, 640));

        KindMerges.Replace(document);

        var configs = document["views"]![0]!["widgets"]!.AsArray().Select(w => Config(w!)).ToList();
        Assert.Equal([Themia.Core.Model.MusicElement.Cover], configs[0].Shows);
        Assert.Equal([Themia.Core.Model.MusicElement.Elapsed, Themia.Core.Model.MusicElement.Bar, Themia.Core.Model.MusicElement.Length], configs[1].Shows);
    }

    /// <summary>A placed widget's config as the load reads it: the music module's, its set repaired into its order.</summary>
    private static Themia.Core.Model.MusicConfig Config(JsonNode widget)
    {
        var config = Assert.IsType<Themia.Core.Model.MusicConfig>(System.Text.Json.JsonSerializer.Deserialize(widget["config"]!.ToJsonString(), StateJson.WidgetConfig));
        _ = config.Repair();
        return config;
    }
}
