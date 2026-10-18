using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>Which transport button was pressed. A closed set, so an unknown command cannot reach the session.</summary>
public enum MediaTransport
{
    PlayPause = 0,
    Next = 1,
    Previous = 2,
}

/// <summary>One snapshot of whatever Windows is playing.</summary>
/// <param name="State">Which of the five states the session is in. Every one of them has a sentence.</param>
/// <param name="Title">The track title, already collapsed to one displayable line.</param>
/// <param name="Subtitle">Artist, album, or both.</param>
/// <param name="TrackKey">Identity of the loaded track, for deciding whether the art in hand is stale.</param>
/// <param name="Timeline">Position and duration, already folded to a zero-based frame.</param>
/// <param name="AnchoredAt">When <paramref name="Timeline"/> was true, so the position can be advanced from it.</param>
/// <param name="CanPlayPause">Whether the session will accept a play/pause.</param>
/// <param name="CanNext">Whether there is a next track to go to.</param>
/// <param name="CanPrevious">Whether there is a previous track to go back to.</param>
/// <param name="CanSeek">[S-1465] Whether the session takes a new position (a live stream does not).</param>
/// <param name="Artist">[S-1469] The artist alone, for the large tile's own line.</param>
/// <param name="Album">[S-1469] The album alone; empty when it repeats the artist.</param>
/// <param name="Origin">The session's own start time, which a seek is expressed against.</param>
public readonly record struct NowPlaying(
    MusicState State,
    string Title,
    string Subtitle,
    string TrackKey,
    TrackTimeline Timeline,
    DateTimeOffset AnchoredAt,
    bool CanPlayPause,
    bool CanNext,
    bool CanPrevious,
    bool CanSeek = false,
    string Artist = "",
    string Album = "",
    TimeSpan Origin = default)
{
    /// <summary>A state with nothing in it. Used for loading and for every flavour of idle.</summary>
    public static NowPlaying Of(MusicState state) => new(
        state,
        string.Empty,
        string.Empty,
        string.Empty,
        default,
        DateTimeOffset.UtcNow,
        false,
        false,
        false);

    /// <summary>True when there is a track to draw, as opposed to a sentence to draw.</summary>
    public bool HasTrack => State is MusicState.Playing or MusicState.Paused;
}
