using System.Globalization;
using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>What the media session is doing. Each value is a different sentence on the tile.</summary>
public enum MusicState
{
    /// <summary>Before the first snapshot arrives.</summary>
    Loading = 0,

    /// <summary>Windows reports no media session at all: nothing on this PC has claimed the transport keys.</summary>
    NoSession = 1,

    /// <summary>There is a session, but it is stopped or has no track in it.</summary>
    Idle = 2,

    Paused = 3,
    Playing = 4,
}

/// <summary>A track's position within itself, already zero-based.</summary>
/// <param name="Position">How far in, clamped into the track.</param>
/// <param name="Duration">How long the track is, or zero when the session will not say.</param>
public readonly record struct TrackTimeline(TimeSpan Position, TimeSpan Duration);

/// <summary>
/// The parts of a now-playing readout that are arithmetic and strings rather than COM.
/// <para>
/// Pulled out of the view because they are the parts with edge cases and the view is the part that
/// needs a running media session to exercise: a stream with no duration, a player that reports a
/// non-zero start offset, a position anchored before the app was even open, a track with a title
/// and no artist.
/// </para>
/// </summary>
public static class MusicRules
{
    /// <summary>
    /// Fold a raw SMTC timeline into a zero-based one.
    /// <para>
    /// Some players report a non-zero <c>StartTime</c> — a chapter within a longer file, a live
    /// stream windowed to the last few hours — and report <c>Position</c> and <c>EndTime</c> against
    /// the same origin. Taking <c>Position</c> at face value against <c>EndTime</c> then puts the
    /// progress bar most of the way along a track that just started.
    /// </para>
    /// </summary>
    public static TrackTimeline Normalise(TimeSpan start, TimeSpan position, TimeSpan end)
    {
        var duration = end - start;

        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        var offset = position - start;

        if (offset < TimeSpan.Zero)
        {
            offset = TimeSpan.Zero;
        }

        if (offset > duration)
        {
            offset = duration;
        }

        return new TrackTimeline(offset, duration);
    }

    /// <summary>
    /// Where the track has reached by now, given where it was when the session last said so.
    /// <para>
    /// The session pushes a timeline update when something happens to it, not once a second, so a
    /// readout that only showed pushed positions would sit still for a minute at a time. This
    /// advances it by wall-clock elapsed since the anchor — which is what the position <i>is</i>
    /// while a track plays — and is a <b>display</b> interpolation only: the anchor always wins
    /// again the moment the session pushes a new one, so the two can never drift apart permanently.
    /// </para>
    /// <para>
    /// A paused track does not advance, and neither does one whose anchor is in the future, which
    /// is what a clock change between the session's timestamp and this machine's looks like.
    /// </para>
    /// </summary>
    public static TimeSpan Interpolate(
        TrackTimeline timeline,
        DateTimeOffset anchoredAt,
        DateTimeOffset now,
        bool playing)
    {
        if (!playing)
        {
            return timeline.Position;
        }

        var elapsed = now - anchoredAt;

        if (elapsed <= TimeSpan.Zero)
        {
            return timeline.Position;
        }

        var advanced = timeline.Position + elapsed;

        // A track never runs past its own end. Without this a paused-at-the-end session that never
        // pushes again would count upwards forever.
        if (timeline.Duration > TimeSpan.Zero && advanced > timeline.Duration)
        {
            return timeline.Duration;
        }

        return advanced;
    }

    /// <summary>
    /// A position or a length, the way every player writes it: <c>3:07</c>, and <c>1:02:30</c> once
    /// there is an hour to show. Zero-padded minutes only past the hour, as the leading field is
    /// never padded.
    /// </summary>
    public static string Clock(TimeSpan span)
    {
        if (!IsUsable(span))
        {
            return "0:00";
        }

        var total = (int)span.TotalSeconds;
        var hours = total / 3600;
        var minutes = total % 3600 / 60;
        var seconds = total % 60;

        return hours > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{hours}:{minutes:D2}:{seconds:D2}")
            : string.Create(CultureInfo.InvariantCulture, $"{minutes}:{seconds:D2}");
    }

    /// <summary>
    /// How far along, 0..1. A track with no duration — a live stream, a player that will not say —
    /// reports 0 rather than a bar that is either always full or always empty by accident.
    /// </summary>
    public static double Progress(TimeSpan position, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero || !IsUsable(position))
        {
            return 0;
        }

        return Math.Clamp(position.TotalSeconds / duration.TotalSeconds, 0, 1);
    }

    /// <summary>
    /// The track title. Collapsed to one line and capped: it comes from whatever app is playing,
    /// which makes it somebody else's string, and it is drawn and nothing else.
    /// </summary>
    public static string Title(string? raw) => Title(raw, Strings.Current);

    /// <inheritdoc cref="Title(string?)"/>
    public static string Title(string? raw, Locale locale) => Line(raw) ?? Strings.Get(locale, Str.WidgetsB_Music_UnknownTrack);

    /// <summary>
    /// The line under the title: the artist, the album, or both. Empty when the session offered
    /// neither, so the view can drop the line rather than reserve space for nothing.
    /// </summary>
    public static string Subtitle(string? artist, string? album)
    {
        var who = Line(artist);
        var what = Line(album);

        if (who is not null && what is not null)
        {
            // The separator Windows' own media controls use between the two.
            return who.Equals(what, StringComparison.OrdinalIgnoreCase) ? who : $"{who} · {what}";
        }

        return who ?? what ?? string.Empty;
    }

    /// <summary>
    /// Identity of the track currently loaded, for deciding whether the album art in hand is still
    /// the right picture. Title, artist and album together: a track number would be better and no
    /// session reliably reports one.
    /// </summary>
    public static string TrackKey(string? title, string? artist, string? album)
        => string.Join('', Line(title) ?? string.Empty, Line(artist) ?? string.Empty, Line(album) ?? string.Empty);

    /// <summary>The sentence shown when there is nothing to draw. No state is a blank tile.</summary>
    public static string StatusMessage(MusicState state) => StatusMessage(state, Strings.Current);

    /// <inheritdoc cref="StatusMessage(MusicState)"/>
    public static string StatusMessage(MusicState state, Locale locale) => state switch
    {
        MusicState.Loading => Strings.Get(locale, Str.WidgetsB_Music_Loading),
        MusicState.NoSession => Strings.Get(locale, Str.WidgetsB_Music_NoSession),
        MusicState.Idle => Strings.Get(locale, Str.WidgetsB_Music_Idle),
        _ => string.Empty,
    };

    /// <summary>
    /// [S-1464] Whether the tile paints nothing. <see cref="MusicHideWhenIdle.NoSession"/> hides only
    /// when Windows reports no session at all; <see cref="MusicHideWhenIdle.NotPlaying"/> also hides
    /// a paused or empty one. Before the first snapshot (loading) a hiding tile stays hidden, so it
    /// does not flash up at every start. Edit mode is the card's rule, not this one: it always shows.
    /// </summary>
    public static bool Hides(Model.MusicHideWhenIdle mode, MusicState state) => mode switch
    {
        Model.MusicHideWhenIdle.NoSession => state is MusicState.NoSession or MusicState.Loading,
        Model.MusicHideWhenIdle.NotPlaying => state != MusicState.Playing,
        _ => false,
    };

    /// <summary>
    /// [S-1465] The fraction of the bar under a pointer at <paramref name="x"/> on a bar
    /// <paramref name="width"/> wide, 0..1. Off either end is the end, and a bar not laid out yet is 0.
    /// </summary>
    public static double ScrubFraction(double x, double width)
        => width > 0 && double.IsFinite(x) ? Math.Clamp(x / width, 0, 1) : 0;

    /// <summary>[S-1465] The position a fraction of the bar stands for. Nothing to seek in a track with no length.</summary>
    public static TimeSpan SeekTarget(double fraction, TimeSpan duration)
        => duration > TimeSpan.Zero && double.IsFinite(fraction)
            ? TimeSpan.FromTicks((long)(duration.Ticks * Math.Clamp(fraction, 0, 1)))
            : TimeSpan.Zero;

    /// <summary>[S-1468] Shift+arrows: five seconds either way, kept inside the track.</summary>
    public static readonly TimeSpan NudgeStep = TimeSpan.FromSeconds(5);

    /// <summary>[S-1468] <paramref name="position"/> moved by <paramref name="delta"/>, clamped into the track.</summary>
    public static TimeSpan Nudge(TimeSpan position, TimeSpan delta, TimeSpan duration)
    {
        var target = position + delta;

        if (target < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return duration > TimeSpan.Zero && target > duration ? duration : target;
    }

    /// <summary>The artist alone, collapsed like every other line; empty when absent.</summary>
    public static string Artist(string? raw) => Line(raw) ?? string.Empty;

    /// <summary>The album alone; empty when absent or the same as the artist (a self-titled release says it once).</summary>
    public static string Album(string? artist, string? album)
    {
        var what = Line(album);
        return what is null || string.Equals(what, Line(artist), StringComparison.OrdinalIgnoreCase) ? string.Empty : what;
    }

    /// <summary>
    /// Collapse somebody else's string to one displayable line, or null when there is nothing in it.
    /// </summary>
    private static string? Line(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var words = raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (words.Length == 0)
        {
            return null;
        }

        var collapsed = string.Join(' ', words);
        return collapsed.Length <= 120 ? collapsed : collapsed[..120];
    }

    private static bool IsUsable(TimeSpan span) => span > TimeSpan.Zero;
}
