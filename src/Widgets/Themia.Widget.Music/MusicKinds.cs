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
[assembly: ThemiaWidget("musicElapsed", typeof(MusicElapsedModule))]
[assembly: ThemiaWidget("musicDuration", typeof(MusicDurationModule))]

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
    public const string Title = "title", Artist = "artist", Album = "album", Elapsed = "elapsed", Duration = "duration";

    public static IReadOnlyList<TextKind> Texts { get; } =
    [
        new("music", Title, Glyphs.Music, MusicStr.Music_Title_Label.Local(), MusicStr.Music_Title_Desc.Local(),
            ["song", "title", "track", "now playing", "spotify", "player", "music"], () => new MusicConfig())
        {
            Lead = false,
            Opens = TextOpens.Player,
        },
        new("musicArtist", Artist, Glyphs.Contact, MusicStr.Music_Artist_Label.Local(), MusicStr.Music_Artist_Desc.Local(),
            ["artist", "band", "singer", "now playing", "music"], () => new MusicArtistConfig())
        {
            Lead = false,
            Tone = TextTone.Secondary,
            None = MusicStr.Music_Artist_None.Local(),
            Opens = TextOpens.Player,
        },
        new("musicAlbum", Album, Glyphs.Disc, MusicStr.Music_Album_Label.Local(), MusicStr.Music_Album_Desc.Local(),
            ["album", "record", "release", "now playing", "music"], () => new MusicAlbumConfig())
        {
            Lead = false,
            Tone = TextTone.Secondary,
            None = MusicStr.Music_Album_None.Local(),
            Opens = TextOpens.Player,
        },
        new("musicElapsed", Elapsed, Glyphs.Timer, MusicStr.Music_Elapsed_Label.Local(), MusicStr.Music_Elapsed_Desc.Local(),
            ["elapsed", "position", "time", "progress", "now playing", "music"], () => new MusicElapsedConfig())
        {
            Lead = false,
            Tone = TextTone.Secondary,
        },
        new("musicDuration", Duration, Glyphs.Clock, MusicStr.Music_Duration_Label.Local(), MusicStr.Music_Duration_Desc.Local(),
            ["duration", "length", "total", "time", "progress", "now playing", "music"], () => new MusicDurationConfig())
        {
            Lead = false,
            Tone = TextTone.Secondary,
        },
    ];

    /// <summary>[S-15106] Whether the text kind <paramref name="key"/> is a time of the track (the elapsed time, its length): a running reading.</summary>
    public static bool IsTime(string key) => key is Elapsed or Duration;

    /// <summary>[S-15106] What a time shows with no time to show: the clock's shape, drawn unavailable (WR-10).</summary>
    public const string NoTime = "-:--";

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

    /// <summary>
    /// A kind's place in the family's order, its add-page rank: the title, the artist, the album, the three buttons, the
    /// cover (6, its own project), then the progress's three parts: the elapsed time, the bar (8, its own project) and the
    /// track's length.
    /// </summary>
    public static int Rank(string key) => key switch
    {
        Title => 0,
        Artist => 1,
        Album => 2,
        Elapsed => 7,
        _ => 9,
    };

    /// <inheritdoc cref="Rank(string)"/>
    public static int Rank(MediaTransport transport) => 3 + Buttons.ToList().IndexOf(transport);

    /// <summary>
    /// [S-14921] What the text kind <paramref name="key"/> shows of <paramref name="now"/>: the title, the artist or the album
    /// of the track; without a track the title says why ("Nothing playing", drawn unavailable) and the artist and the album
    /// their own words for none, the why their tooltip; a track with no artist or no album (a stream, a video) the same.
    /// The app that owns the session is what a click opens ([S-2030]): none before the first read or with no session.
    /// </summary>
    /// <param name="key">Which text.</param>
    /// <param name="now">What is playing.</param>
    /// <param name="position">[S-15106] How far into the track it is (the elapsed time's text).</param>
    public static TextShown Shown(string key, NowPlaying now, TimeSpan position = default)
    {
        var owner = now.State is MusicState.NoSession or MusicState.Loading ? null : now.AppId;
        if (IsTime(key))
        {
            return Time(key, now, position);
        }

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

    /// <summary>
    /// [S-15106] What a time shows of <paramref name="now"/>: the elapsed time at <paramref name="position"/> or the
    /// track's length, as the clock writes them (<see cref="MusicRules.Clock"/>); with no track, and the length of a
    /// stream with no end, the clock's shape with no figures, unavailable, why its tooltip (WR-10: the face stays).
    /// </summary>
    private static TextShown Time(string key, NowPlaying now, TimeSpan position)
    {
        if (!now.HasTrack)
        {
            return new TextShown(NoTime, false) { Why = MusicRules.StatusMessage(now.State) };
        }

        if (key == Elapsed)
        {
            return new TextShown(MusicRules.Clock(position));
        }

        return now.Timeline.Duration > TimeSpan.Zero ? new TextShown(MusicRules.Clock(now.Timeline.Duration)) : new TextShown(NoTime, false);
    }
}

// [S-14920] Each kind is its module and its config (a config's type is how it knows its id, [S-6902]); everything else is
// the family's (MusicTextModule and MusicButtonModule, the views) over the text base and the icon base.

public sealed class MusicTitleModule() : MusicTextModule(MusicKinds.Title);

public sealed class MusicArtistModule() : MusicTextModule(MusicKinds.Artist);

public sealed class MusicAlbumModule() : MusicTextModule(MusicKinds.Album);

public sealed class MusicElapsedModule() : MusicTextModule(MusicKinds.Elapsed);

public sealed class MusicDurationModule() : MusicTextModule(MusicKinds.Duration);

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
[JsonSerializable(typeof(MusicElapsedConfig))]
[JsonSerializable(typeof(MusicDurationConfig))]
[JsonSerializable(typeof(MusicPreviousConfig))]
[JsonSerializable(typeof(MusicPlayPauseConfig))]
[JsonSerializable(typeof(MusicNextConfig))]
internal sealed partial class MusicJsonContext : JsonSerializerContext;
