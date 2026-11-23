using System.Text.Json;
using Themia.App.Services;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Music.Tests;

/// <summary>[S-16500] [S-16501] [S-16508] Now playing's config (its style, its element set, its repair) and what its face says.</summary>
public sealed class MusicConfigTests
{
    [Fact]
    public void S16500_a_new_card_is_the_side_cover_showing_every_element()
    {
        var config = new MusicConfig();
        Assert.Equal(MusicStyle.Side, config.Style);
        Assert.Equal(MusicElements.All, config.Shows);
        Assert.Equal(MusicHideWhenIdle.Never, config.HideWhenIdle);
        Assert.Empty(config.Repair());
    }

    [Fact]
    public void S16501_the_set_is_stored_as_shown_in_camel_case_and_read_back()
    {
        var config = new MusicConfig { Style = MusicStyle.FullArt, Shows = [MusicElement.Cover, MusicElement.PlayPause] };
        var json = JsonSerializer.Serialize<WidgetConfig>(config, StateJson.WidgetConfig).Replace(" ", string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"shows\":[", json, StringComparison.Ordinal);
        Assert.Contains("\"playPause\"", json, StringComparison.Ordinal);
        Assert.Contains("\"style\":\"fullArt\"", json, StringComparison.Ordinal);

        var read = Assert.IsType<MusicConfig>(JsonSerializer.Deserialize(json, StateJson.WidgetConfig));
        Assert.Equal([MusicElement.Cover, MusicElement.PlayPause], read.Shows);
        Assert.Equal(MusicStyle.FullArt, read.Style);
    }

    [Fact]
    public void S16501_an_unknown_or_repeated_element_is_dropped_loudly_and_an_empty_set_is_every_element_again()
    {
        var read = Assert.IsType<MusicConfig>(JsonSerializer.Deserialize("""{"kind":"music","shows":["next","lyrics",3,"title","next"]}""", StateJson.WidgetConfig));
        Assert.Single(read.Repair());
        Assert.Equal([MusicElement.Title, MusicElement.Next], read.Shows);

        var empty = new MusicConfig { Shows = [] };
        Assert.Single(empty.Repair());
        Assert.Equal(MusicElements.All, empty.Shows);

        var hiding = new MusicConfig { HideWhenIdle = (MusicHideWhenIdle)9 };
        Assert.Single(hiding.Repair());
        Assert.Equal(MusicHideWhenIdle.Never, hiding.HideWhenIdle);
    }

    [Fact]
    public void S16501_a_toggle_keeps_the_canonical_order_and_never_hides_the_last_element()
    {
        Assert.Equal([MusicElement.Cover, MusicElement.Next], MusicElements.Toggle([MusicElement.Next], MusicElement.Cover, on: true));
        Assert.Equal([MusicElement.Next], MusicElements.Toggle([MusicElement.Cover, MusicElement.Next], MusicElement.Cover, on: false));
        Assert.Equal([MusicElement.Next], MusicElements.Toggle([MusicElement.Next], MusicElement.Next, on: false));
    }

    [Fact]
    public void S16500_a_copy_keeps_the_style_the_set_and_the_hiding_and_shares_no_list()
    {
        var config = new MusicConfig { Style = MusicStyle.Strip, Shows = [MusicElement.Title], HideWhenIdle = MusicHideWhenIdle.NotPlaying };
        var copy = Assert.IsType<MusicConfig>(config.Clone());
        Assert.Equal((MusicStyle.Strip, MusicHideWhenIdle.NotPlaying), (copy.Style, copy.HideWhenIdle));
        Assert.Equal([MusicElement.Title], copy.Shows);
        Assert.NotSame(config.Shows, copy.Shows);
    }

    [Fact]
    public void S16502_the_sizing_follows_the_style_and_the_set()
    {
        var module = new MusicModule();
        var side = module.Descriptor.Sizing(new MusicConfig());
        var strip = module.Descriptor.Sizing(new MusicConfig { Style = MusicStyle.Strip });
        var transport = module.Descriptor.Sizing(new MusicConfig { Shows = [MusicElement.Previous, MusicElement.PlayPause, MusicElement.Next] });
        Assert.Equal(MusicPlan.Sizing(MusicStyle.Side, MusicElements.All).Natural, side.Natural);
        Assert.True(strip.Natural.Height < side.Natural.Height);
        Assert.Equal(new SizeDip(12 + 100 + 12, 12 + 28 + 12), transport.Natural);
        Assert.Equal(SizeStretch.Ceiling, side.Max.Width);
    }

    private static NowPlaying Track => new(MusicState.Paused, "Northern Lights", "Aurora Quartet · Long Winter", "k",
        new TrackTimeline(TimeSpan.FromSeconds(74), TimeSpan.FromSeconds(222)), DateTimeOffset.UnixEpoch, true, true, true,
        Artist: "Aurora Quartet", Album: "Long Winter");

    [Fact]
    public void S16508_the_texts_say_the_track_with_the_artist_and_album_that_show()
    {
        Assert.Equal(("Northern Lights", "Aurora Quartet · Long Winter"), MusicWords.Lines(Track, artist: true, album: true, subtitleShown: true));
        Assert.Equal(("Northern Lights", "Aurora Quartet"), MusicWords.Lines(Track, artist: true, album: false, subtitleShown: true));
        Assert.Equal(("Northern Lights", "Long Winter"), MusicWords.Lines(Track, artist: false, album: true, subtitleShown: true));
        Assert.Equal(("1:14", "3:42"), MusicWords.Times(Track, TimeSpan.FromSeconds(74)));
        Assert.Equal(("1:14", MusicWords.NoTime), MusicWords.Times(Track with { Timeline = new TrackTimeline(TimeSpan.FromSeconds(74), TimeSpan.Zero) }, TimeSpan.FromSeconds(74)));
    }

    [Fact]
    public void S16508_with_nothing_to_play_the_face_stays_with_dashes_and_says_why_in_one_line()
    {
        // WR-10 (owner, 2026-11-19): "its reading as '--'" and "says why in a compact line".
        var idle = NowPlaying.Of(MusicState.Idle);
        Assert.Equal((MusicWords.NoReading, MusicRules.StatusMessage(MusicState.Idle)), MusicWords.Lines(idle, true, true, subtitleShown: true));
        Assert.Equal((MusicRules.StatusMessage(MusicState.NoSession), string.Empty), MusicWords.Lines(NowPlaying.Of(MusicState.NoSession), false, false, subtitleShown: false));
        Assert.Equal((MusicWords.NoTime, MusicWords.NoTime), MusicWords.Times(idle, TimeSpan.Zero));

        // The template holds "-:--" as it holds every time.
        Assert.True(Themia.Core.Widgets.HeroFit.Ems(MusicWords.NoTime) <= Themia.Core.Widgets.HeroFit.Ems(MusicPlan.TimeTemplate(TimeSpan.Zero)));
    }

    [Fact]
    public void S11321_the_picture_is_read_per_track_and_per_reading_of_the_media_properties()
    {
        static NowPlaying At(string key, int media, MusicState state = MusicState.Playing)
            => new(state, "t", "s", key, default, DateTimeOffset.UnixEpoch, true, true, true, Media: media);

        // The same track, the same reading: nothing to read again (a position or a play/pause notice).
        Assert.Equal(MusicWords.ArtKey(At("a", 3)), MusicWords.ArtKey(At("a", 3, MusicState.Paused)));

        // The same track after its media properties changed (a browser's picture arriving): read again.
        Assert.NotEqual(MusicWords.ArtKey(At("a", 3)), MusicWords.ArtKey(At("a", 4)));

        // Another track: read again. A key never reads as another track's reading ("1" + "2a" against "12" + "a").
        Assert.NotEqual(MusicWords.ArtKey(At("a", 3)), MusicWords.ArtKey(At("b", 3)));
        Assert.NotEqual(MusicWords.ArtKey(At("2a", 1)), MusicWords.ArtKey(At("a", 12)));

        // Nothing playing: no picture to read.
        Assert.Equal(string.Empty, MusicWords.ArtKey(NowPlaying.Of(MusicState.Idle)));
    }

    [Fact]
    public void S16513_the_parts_fold_back_with_what_each_showed()
    {
        var merges = new MusicModule().Descriptor.Merges;
        Assert.All(merges, m => Assert.Equal(MergeFold.Touching, m.Fold));
        string[] Of(string kind) => [.. merges.Single(m => m.Kind == kind).Shows!.Value.Values];
        Assert.Equal(["cover"], Of("musicCover"));
        Assert.Equal(["elapsed", "bar", "length"], Of("musicProgress"));
        Assert.Equal(["title", "artist", "album", "previous", "playPause", "next"], Of("music"));
        Assert.All(merges, m => Assert.Equal("shows", m.Shows!.Value.Key));
    }
}
