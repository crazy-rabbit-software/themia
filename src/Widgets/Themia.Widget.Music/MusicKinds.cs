using System.Text.Json.Serialization;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Music;

[assembly: ThemiaWidgetFamily]
[assembly: ThemiaWidget("music", typeof(MusicTitleModule))]
[assembly: ThemiaWidget("musicArtist", typeof(MusicArtistModule))]
[assembly: ThemiaWidget("musicAlbum", typeof(MusicAlbumModule))]
[assembly: ThemiaWidget("musicPrevious", typeof(MusicPreviousModule))]
[assembly: ThemiaWidget("musicPlayPause", typeof(MusicPlayPauseModule))]
[assembly: ThemiaWidget("musicNext", typeof(MusicNextModule))]

namespace Themia.Widget.Music;

/// <summary>
/// [S-14920] The now-playing family (owner, 2026-11-21: "now playing widget should be split into the individual buttons, the
/// title and the album name."): what Windows is playing, a widget a part. Its texts are kinds of the text base ([S-14934]):
/// the title (<c>music</c>, the old track atom's id, so a placed one is the title), the artist and the album; its buttons
/// (previous, play or pause, next) are an icon each ([S-14922], on the icon base, [S-15003]). The cover and the progress are their own projects, in the
/// same add-page family. The order here is the family's on the add page.
/// </summary>
internal static class MusicKinds
{
    /// <summary>The text kinds' keys: which text of the session each shows.</summary>
    public const string Title = "title", Artist = "artist", Album = "album";

    public static IReadOnlyList<TextKind> Texts { get; } =
    [
        new("music", Title, Glyphs.Music, MusicStr.Music_Title_Label.Local(), MusicStr.Music_Title_Desc.Local(),
            ["song", "title", "track", "now playing", "spotify", "player", "music"], () => new MusicConfig())
        {
            Opens = TextOpens.Player,
        },
        new("musicArtist", Artist, Glyphs.Contact, MusicStr.Music_Artist_Label.Local(), MusicStr.Music_Artist_Desc.Local(),
            ["artist", "band", "singer", "now playing", "music"], () => new MusicArtistConfig())
        {
            Tone = TextTone.Secondary,
            None = MusicStr.Music_Artist_None.Local(),
            Opens = TextOpens.Player,
        },
        new("musicAlbum", Album, Glyphs.Disc, MusicStr.Music_Album_Label.Local(), MusicStr.Music_Album_Desc.Local(),
            ["album", "record", "release", "now playing", "music"], () => new MusicAlbumConfig())
        {
            Tone = TextTone.Secondary,
            None = MusicStr.Music_Album_None.Local(),
            Opens = TextOpens.Player,
        },
    ];

    /// <summary>The text kind showing <paramref name="key"/>.</summary>
    public static TextKind Text(string key) => Texts.Single(k => k.Key == key);

    /// <summary>The buttons, in the transport's order after the texts.</summary>
    public static IReadOnlyList<MediaTransport> Buttons { get; } = [MediaTransport.Previous, MediaTransport.PlayPause, MediaTransport.Next];

    /// <summary>[S-14922] The buttons as icon kinds ([S-15003]): each its id, its glyph at rest, its words and its config.</summary>
    public static IReadOnlyList<IconKind> ButtonKinds { get; } =
    [
        ButtonKind("musicPrevious", MediaTransport.Previous, () => new MusicPreviousConfig()),
        ButtonKind("musicPlayPause", MediaTransport.PlayPause, () => new MusicPlayPauseConfig()),
        ButtonKind("musicNext", MediaTransport.Next, () => new MusicNextConfig()),
    ];

    /// <summary>The icon kind of the button that asks <paramref name="transport"/>.</summary>
    public static IconKind Button(MediaTransport transport) => ButtonKinds[Buttons.ToList().IndexOf(transport)];

    private static IconKind ButtonKind(string id, MediaTransport transport, Func<MusicButtonConfig> create)
        => new(id, MusicButtons.Glyph(transport, playing: false), MusicButtons.Label(transport), MusicButtons.Description(transport),
            ["media controls", "transport", "playback", "now playing", "music", .. MusicButtons.Words(transport)], create);

    /// <summary>A kind's place in the family's order: its add-page rank (the cover and the progress follow them).</summary>
    public static int Rank(string key) => Texts.ToList().FindIndex(k => k.Key == key);

    /// <inheritdoc cref="Rank(string)"/>
    public static int Rank(MediaTransport transport) => Texts.Count + Buttons.ToList().IndexOf(transport);

    /// <summary>
    /// [S-14921] What the text kind <paramref name="key"/> shows of <paramref name="now"/>: the title, the artist or the album
    /// of the track; without a track the title says why ("Nothing playing", drawn unavailable) and the artist and the album
    /// their own words for none, the why their tooltip; a track with no artist or no album (a stream, a video) the same.
    /// The app that owns the session is what a click opens ([S-2030]): none before the first read or with no session.
    /// </summary>
    public static TextShown Shown(string key, NowPlaying now)
    {
        var owner = now.State is MusicState.NoSession or MusicState.Loading ? null : now.AppId;
        if (!now.HasTrack)
        {
            var why = MusicRules.StatusMessage(now.State);
            return key == Title ? new TextShown(why, false) { Owner = owner } : TextShown.None(why) with { Owner = owner };
        }

        var text = key switch
        {
            Title => now.Title,
            Artist => MusicRules.Artist(now.Artist),
            _ => MusicRules.Album(now.Artist, now.Album),
        };
        return text.Length > 0 ? new TextShown(text) { Owner = owner } : TextShown.None() with { Owner = owner };
    }
}

// [S-14920] Each kind is its module and its config (a config's type is how it knows its id, [S-6902]); everything else is
// the family's (MusicTextModule and MusicButtonModule, the views) over the text base and the icon base.

public sealed class MusicTitleModule() : MusicTextModule(MusicKinds.Title);

public sealed class MusicArtistModule() : MusicTextModule(MusicKinds.Artist);

public sealed class MusicAlbumModule() : MusicTextModule(MusicKinds.Album);

public sealed class MusicPreviousModule() : MusicButtonModule(MediaTransport.Previous);

public sealed class MusicPlayPauseModule() : MusicButtonModule(MediaTransport.PlayPause);

public sealed class MusicNextModule() : MusicButtonModule(MediaTransport.Next);

/// <summary>[S-6903] The family's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MusicConfig))]
[JsonSerializable(typeof(MusicArtistConfig))]
[JsonSerializable(typeof(MusicAlbumConfig))]
[JsonSerializable(typeof(MusicPreviousConfig))]
[JsonSerializable(typeof(MusicPlayPauseConfig))]
[JsonSerializable(typeof(MusicNextConfig))]
internal sealed partial class MusicJsonContext : JsonSerializerContext;
