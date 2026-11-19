using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-1465] [S-12816] The now-playing seek bar's face: a thin track and its fill, and a thumb that shows only under the
/// pointer or while scrubbing a seekable track. The bar is as tall as the thumb, so the thumb never grows it, and its
/// ink is its track's at rest and under the pointer alike: what the block it stands in is scaled and placed by
/// (<see cref="BlockFit"/>) never depends on the pointer (owner, 2026-11-19: "the now playing scrubber changes text
/// sizes when hovering over it with the mouse … this should not be").
/// </summary>
public static class SeekBarFace
{
    /// <summary>The visible line.</summary>
    public const double Track = FamilyShape.Track;

    /// <summary>The thumb shown while hovering or scrubbing a seekable track.</summary>
    public const double Thumb = 12;

    /// <summary>The height the bar takes: the thumb's, so the thumb is never clipped and is easy to hit.</summary>
    public const double Height = Thumb;

    /// <summary>
    /// The room over and under the bar's ink in its box: the track's, centred in the bar, whether or not the thumb
    /// shows. The thumb is drawn over that ink under the pointer and never moves it.
    /// </summary>
    public static LineInk Ink => new((Height - Track) / 2, (Height - Track) / 2);

    /// <summary>Whether the thumb shows: over a track that can seek, while the pointer is on the bar or scrubs it.</summary>
    public static bool ThumbShown(bool canSeek, bool pointed, bool scrubbing) => canSeek && (pointed || scrubbing);
}
