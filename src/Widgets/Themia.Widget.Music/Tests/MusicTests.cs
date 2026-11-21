using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Links;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Music.Tests;

/// <summary>[S-14920]..[S-14922] [S-15106] [S-15108] [S-15109] The now-playing family: a widget a part, its texts and times atoms on the universal spacing, its buttons on the icon base.</summary>
public sealed class MusicTests
{
    private static readonly IWidgetModule[] Modules =
    [
        new MusicTitleModule(), new MusicArtistModule(), new MusicAlbumModule(),
        new MusicPreviousModule(), new MusicPlayPauseModule(), new MusicNextModule(),
        new MusicElapsedModule(), new MusicDurationModule(),
    ];

    private static NowPlaying Track(MusicState state = MusicState.Paused) => new(
        state, "Northern Lights", "Aurora Quartet, Long Winter", "k", default, DateTimeOffset.UnixEpoch,
        CanPlayPause: true, CanNext: true, CanPrevious: false, Artist: "Aurora Quartet", Album: "Long Winter", AppId: "Spotify.exe");

    [Fact]
    public void S14920_the_title_keeps_the_old_id_and_every_part_is_in_the_now_playing_family_in_its_order()
    {
        Assert.Equal(["music", "musicArtist", "musicAlbum", "musicPrevious", "musicPlayPause", "musicNext", "musicElapsed", "musicDuration"], Modules.Select(m => m.Descriptor.Id.Value));
        Assert.All(Modules, m => Assert.Equal(AddFolderId.NowPlaying, m.Descriptor.Add.Shelf));

        // The cover is 6 and the bar 8 (their own projects): the progress's parts stand elapsed, bar, length.
        Assert.Equal([0, 1, 2, 3, 4, 5, 7, 9], Modules.Select(m => m.Descriptor.Add.Rank));
        Assert.All(Modules, m => Assert.True(m.Descriptor.Traits.HasFlag(WidgetTraits.OneReadout)));
        Assert.Equal(typeof(MusicConfig), Modules[0].Descriptor.ConfigType);
    }

    [Fact]
    public void S14921_the_texts_are_the_tracks_title_artist_and_album_from_the_app_that_plays_it()
    {
        var now = Track();

        Assert.Equal(new TextShown("Northern Lights") { Owner = "Spotify.exe" }, MusicKinds.Shown(MusicKinds.Title, now));
        Assert.Equal("Aurora Quartet", MusicKinds.Shown(MusicKinds.Artist, now).Text);
        Assert.Equal("Long Winter", MusicKinds.Shown(MusicKinds.Album, now).Text);
        Assert.All([MusicKinds.Title, MusicKinds.Artist, MusicKinds.Album], key => Assert.True(MusicKinds.Shown(key, now).Available));
    }

    [Fact]
    public void S14921_without_a_track_the_title_says_why_and_the_artist_and_album_their_words_for_none()
    {
        var idle = NowPlaying.Of(MusicState.Idle) with { AppId = "Spotify.exe" };

        var title = MusicKinds.Shown(MusicKinds.Title, idle);
        Assert.Equal((MusicRules.StatusMessage(MusicState.Idle), false), (title.Text, title.Available));
        Assert.Equal("Spotify.exe", title.Owner);

        var album = MusicKinds.Shown(MusicKinds.Album, idle);
        Assert.Equal(("No album", false), TextRules.Drawn(album, MusicKinds.Text(MusicKinds.Album).None!.Value.Text()));
        Assert.Equal(MusicRules.StatusMessage(MusicState.Idle), album.Why);

        // Before the first read and with no session there is no player to open.
        Assert.Null(MusicKinds.Shown(MusicKinds.Title, NowPlaying.Of(MusicState.NoSession) with { AppId = "Spotify.exe" }).Owner);
    }

    [Fact]
    public void S14921_a_track_with_no_artist_or_album_says_none_and_a_self_titled_album_is_said_once()
    {
        var bare = Track() with { Artist = string.Empty, Album = string.Empty };
        Assert.False(MusicKinds.Shown(MusicKinds.Artist, bare).Available);
        Assert.False(MusicKinds.Shown(MusicKinds.Album, bare).Available);
        Assert.False(MusicKinds.Shown(MusicKinds.Album, Track() with { Album = "Aurora Quartet" }).Available);
    }

    [Fact]
    public void S14921_the_texts_are_one_line_with_no_lead_the_artist_and_album_secondary()
    {
        var title = MusicKinds.Text(MusicKinds.Title);
        Assert.Equal((1, false, TextTone.Primary), (title.Lines, title.Lead, title.Tone));
        Assert.All(MusicKinds.Texts, k => Assert.False(k.Lead));
        Assert.All(MusicKinds.Texts.Skip(1), k => Assert.Equal(TextTone.Secondary, k.Tone));
        Assert.All([MusicKinds.Title, MusicKinds.Artist, MusicKinds.Album], key => Assert.Same(TextOpens.Player, MusicKinds.Text(key).Opens));
    }

    [Fact]
    public void S15108_each_text_is_a_text_atom_of_one_new_height_the_times_short()
    {
        var text = AtomRules.TextSizing(MusicTextModule.TextWidth, AtomRules.TextRoom).Natural;
        var time = AtomRules.TextSizing(MusicTextModule.TimeWidth, AtomRules.TextRoom).Natural;
        Assert.Equal(new SizeDip(240, 30), text);
        Assert.Equal(new SizeDip(60, 30), time);
        Assert.All(Modules.Take(3), m => Assert.Equal(text, m.Descriptor.Sizing(m.Descriptor.CreateDefault()).Natural));
        Assert.All(Modules.Skip(6), m => Assert.Equal(time, m.Descriptor.Sizing(m.Descriptor.CreateDefault()).Natural));

        // [S-12411] A taller card is a larger text: no height is fixed.
        Assert.All(Modules, m => Assert.False(m.Descriptor.Sizing(m.Descriptor.CreateDefault()).FixedHeight));
    }

    [Fact]
    public void S15106_the_times_are_the_elapsed_time_and_the_tracks_length_as_the_clock_writes_them()
    {
        var now = Track() with { Timeline = new TrackTimeline(TimeSpan.FromSeconds(74), TimeSpan.FromSeconds(222)) };

        Assert.Equal(new TextShown("1:14"), MusicKinds.Shown(MusicKinds.Elapsed, now, TimeSpan.FromSeconds(74)));
        Assert.Equal(new TextShown("3:42"), MusicKinds.Shown(MusicKinds.Duration, now, TimeSpan.FromSeconds(74)));
        Assert.True(MusicKinds.IsTime(MusicKinds.Elapsed) && MusicKinds.IsTime(MusicKinds.Duration) && !MusicKinds.IsTime(MusicKinds.Title));

        // A time opens nothing: it is a reading, not a name.
        Assert.Null(MusicKinds.Text(MusicKinds.Elapsed).Opens);
        Assert.Null(MusicKinds.Text(MusicKinds.Duration).Opens);
    }

    [Fact]
    public void S15106_with_no_track_or_no_end_a_time_keeps_its_shape_unavailable()
    {
        var idle = NowPlaying.Of(MusicState.Idle);
        foreach (var key in new[] { MusicKinds.Elapsed, MusicKinds.Duration })
        {
            var shown = MusicKinds.Shown(key, idle);
            Assert.Equal((MusicKinds.NoTime, false), (shown.Text, shown.Available));
            Assert.Equal(MusicRules.StatusMessage(MusicState.Idle), shown.Why);
        }

        // A stream with no end: the elapsed time runs, the length has nothing to say.
        var stream = Track(MusicState.Playing);
        Assert.Equal(new TextShown("0:05"), MusicKinds.Shown(MusicKinds.Elapsed, stream, TimeSpan.FromSeconds(5)));
        Assert.Equal(new TextShown(MusicKinds.NoTime, false), MusicKinds.Shown(MusicKinds.Duration, stream));
    }

    [Fact]
    public void S15109_the_elapsed_time_follows_a_scrub_of_its_own_track_alone()
    {
        var now = Track(MusicState.Playing);
        var played = TimeSpan.FromSeconds(74);
        var pointer = TimeSpan.FromSeconds(150);

        // No scrub: the session's position. A scrub of this track: where the pointer has it.
        Assert.Equal(played, MusicKinds.Position(now, played, null));
        Assert.Equal(pointer, MusicKinds.Position(now, played, new TrackLink(now.TrackKey, pointer)));
        Assert.Equal(new TextShown("2:30"), MusicKinds.Shown(MusicKinds.Elapsed, now, MusicKinds.Position(now, played, new TrackLink(now.TrackKey, pointer))));

        // Another track's scrub, or one while nothing plays here, moves nothing.
        Assert.Equal(played, MusicKinds.Position(now, played, new TrackLink("another", pointer)));
        Assert.Equal(played, MusicKinds.Position(NowPlaying.Of(MusicState.Idle), played, new TrackLink(string.Empty, pointer)));
    }

    [Fact]
    public void S15109_the_elapsed_time_alone_follows_the_scrub_on_the_views_links()
    {
        Assert.Equal([LinkTopicId.ScrubbedTrack], Modules[6].Descriptor.Links.Follows);
        Assert.Empty(Modules[6].Descriptor.Links.Publishes);
        Assert.All(Modules.Where((_, i) => i != 6), m => Assert.Empty(m.Descriptor.Links.Topics));
    }

    [Fact]
    public void S15106_a_text_sits_at_the_start_and_the_tracks_length_at_the_end_unless_chosen()
    {
        Assert.Equal(ContentAlign.Left, ((IContentAligned)new MusicConfig()).DefaultAlign);
        Assert.Equal(ContentAlign.Left, ((IContentAligned)new MusicElapsedConfig()).DefaultAlign);
        Assert.Equal(ContentAlign.Right, ((IContentAligned)new MusicDurationConfig()).DefaultAlign);
        Assert.IsType<MusicDurationConfig>(new MusicDurationConfig().Clone());
    }

    [Fact]
    public void S14922_play_or_pause_says_what_a_click_does_and_is_the_accent_while_it_plays()
    {
        var paused = MusicButtons.Shown(MediaTransport.PlayPause, Track());
        Assert.Equal((Glyphs.Play, "Play", TileState.Off), (paused.Glyph, paused.Name, paused.State));

        var playing = MusicButtons.Shown(MediaTransport.PlayPause, Track(MusicState.Playing));
        Assert.Equal((Glyphs.Pause, "Pause", TileState.On), (playing.Glyph, playing.Name, playing.State));
    }

    [Fact]
    public void S14922_a_button_the_player_cannot_do_and_every_button_without_a_track_is_unavailable()
    {
        Assert.Equal(TileState.Unavailable, MusicButtons.Shown(MediaTransport.Previous, Track()).State);
        Assert.Equal(TileState.Off, MusicButtons.Shown(MediaTransport.Next, Track()).State);
        Assert.All(MusicKinds.Buttons, t => Assert.Equal(TileState.Unavailable, MusicButtons.Shown(t, NowPlaying.Of(MusicState.Idle)).State));
        Assert.Equal(Glyphs.Next, MusicButtons.Shown(MediaTransport.Next, NowPlaying.Of(MusicState.Idle)).Glyph);

        // [S-15001] Without a track, why is its tooltip; with one it can't do, its name.
        Assert.Equal(MusicRules.StatusMessage(MusicState.Idle), MusicButtons.Shown(MediaTransport.Next, NowPlaying.Of(MusicState.Idle)).Tooltip);
        Assert.Equal("Previous", MusicButtons.Shown(MediaTransport.Previous, Track()).Tooltip);
    }

    [Fact]
    public void S14922_a_button_is_an_icon_on_the_icon_base()
    {
        Assert.Equal(IconRules.Sizing().Natural, Modules[4].Descriptor.Sizing(new MusicPlayPauseConfig()).Natural);
        Assert.IsAssignableFrom<IconModule>(Modules[4]);
        Assert.Equal(["musicPrevious", "musicPlayPause", "musicNext"], MusicKinds.ButtonKinds.Select(k => k.Id));
        Assert.Equal(MediaTransport.Previous, new MusicPreviousConfig().Transport);
        Assert.Equal(MediaTransport.Next, new MusicNextConfig().Transport);
    }

    [Fact]
    public void S14920_an_unknown_hiding_mode_is_repaired_and_a_copy_keeps_its_kind_and_setting()
    {
        var config = new MusicAlbumConfig { HideWhenIdle = (MusicHideWhenIdle)9 };
        Assert.Single(config.Repair());
        Assert.Equal(MusicHideWhenIdle.Never, config.HideWhenIdle);

        var copy = new MusicNextConfig { HideWhenIdle = MusicHideWhenIdle.NotPlaying }.Clone();
        Assert.IsType<MusicNextConfig>(copy);
        Assert.Equal(MusicHideWhenIdle.NotPlaying, ((MusicPartConfig)copy).HideWhenIdle);
    }
}
