namespace Themia.Core.Widgets;

/// <summary>
/// Where a reading's caption goes: beside the number, or on its own line under it.
/// <para>
/// [S-646] A meter draws its reading and a caption on one line — "46%  30 GB of 64 GB". On a
/// narrow tile there is no room for both, and the caption was trimmed to "30 G…" or "all cor…":
/// an ellipsis where the information should be. Below the width the pair needs, the caption moves
/// under the reading instead, where it has the whole width to itself.
/// </para>
/// </summary>
public static class ReadoutLayout
{
    /// <summary>
    /// True when a reading drawn at <paramref name="reading"/> and its caption drawn at
    /// <paramref name="caption"/> both fit across <paramref name="width"/> with
    /// <paramref name="gap"/> between them. An unmeasured box answers true, which is the layout
    /// the view had before it knew its size.
    /// </summary>
    public static bool CaptionFitsBeside(
        double width,
        TypeStep reading,
        double readingCharacters,
        TypeStep caption,
        double captionCharacters,
        double gap)
    {
        if (!double.IsFinite(width) || width <= 0)
        {
            return true;
        }

        if (captionCharacters <= 0)
        {
            return true;
        }

        var needed = FluentType.EstimateWidth(reading, readingCharacters)
            + gap
            + FluentType.EstimateWidth(caption, captionCharacters);

        return needed <= width;
    }
}
