namespace Themia.Core.Widgets;

/// <summary>
/// [S-1465] [S-12816] [S-15104] The now-playing seek bar's face: a rail that fills its atom's room and its fill, and a
/// thumb that shows only under the pointer or while scrubbing a seekable track thinner than the thumb. The thumb is drawn
/// over the bar and the spacing round it, never laid out: nothing the bar or its neighbours are sized by depends on the
/// pointer (owner, 2026-11-19: "the now playing scrubber changes text sizes when hovering over it with the mouse … this
/// should not be").
/// </summary>
public static class SeekBarFace
{
    /// <summary>The thumb shown while hovering or scrubbing a thin seekable track.</summary>
    public const double Thumb = 12;

    /// <summary>
    /// Whether the thumb shows: over a track that can seek, while the pointer is on the bar or scrubs it, and only on a
    /// rail <paramref name="track"/> thinner than the thumb (a thicker rail's fill is its own handle).
    /// </summary>
    public static bool ThumbShown(bool canSeek, bool pointed, bool scrubbing, double track)
        => canSeek && (pointed || scrubbing) && track < Thumb;
}
