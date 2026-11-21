using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Music.Tests;

/// <summary>[S-14920]..[S-14922] The now-playing family: a widget a part, its texts on the text base, its buttons on the icon base.</summary>
public sealed class MusicTests
{
    private static readonly IWidgetModule[] Modules =
    [
        new MusicTitleModule(), new MusicArtistModule(), new MusicAlbumModule(),
        new MusicPreviousModule(), new MusicPlayPauseModule(), new MusicNextModule(),
    ];

    private static NowPlaying Track(MusicState state = MusicState.Paused) => new(
        state, "Northern Lights", "Aurora Quartet, Long Winter", "k", default, DateTimeOffset.UnixEpoch,
        CanPlayPause: true, CanNext: true, CanPrevious: false, Artist: "Aurora Quartet", Album: "Long Winter", AppId: "Spotify.exe");

    [Fact]
    public void S14920_the_title_keeps_the_old_id_and_every_part_is_in_the_now_playing_family_in_its_order()
    {
        Assert.Equal(["music", "musicArtist", "musicAlbum", "musicPrevious", "musicPlayPause", "musicNext"], Modules.Select(m => m.Descriptor.Id.Value));
        Assert.All(Modules, m => Assert.Equal(AddFolderId.NowPlaying, m.Descriptor.Add.Shelf));
        Assert.Equal([0, 1, 2, 3, 4, 5], Modules.Select(m => m.Descriptor.Add.Rank));
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
    public void S14921_the_texts_are_one_line_led_by_their_glyph_the_artist_and_album_secondary()
    {
        var title = MusicKinds.Text(MusicKinds.Title);
        Assert.Equal((1, true, TextTone.Primary), (title.Lines, title.Lead, title.Tone));
        Assert.Equal(TextTone.Secondary, MusicKinds.Text(MusicKinds.Artist).Tone);
        Assert.Equal(TextTone.Secondary, MusicKinds.Text(MusicKinds.Album).Tone);
        Assert.All(MusicKinds.Texts, k => Assert.Same(TextOpens.Player, k.Opens));
        Assert.Equal(TextRules.Sizing(title).Natural, Modules[0].Descriptor.Sizing(new MusicConfig()).Natural);
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
