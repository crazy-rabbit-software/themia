using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-15710] WR-10 on a one-reading face (an icon, a line of text): without a reading the face stays and the card says why
/// in one compact line under it, the Tile block's state line (a gap and one Caption line, <see cref="TileRules.Below"/>),
/// where the card's content room holds the face's least over that band and a sentence's room across
/// (<see cref="StatusRules.MinSentence"/>). A smaller card keeps the face alone, the why its tooltip, a problem its status
/// mark as a stale reading's ([S-15711]).
/// </summary>
public static class WhyLine
{
    /// <summary>[S-15710] The line's band under the face: the gap and the Caption line, 24.</summary>
    public static double Band => TileRules.Below(label: false, state: true);

    /// <summary>
    /// [S-15710] Whether a content room <paramref name="width"/> by <paramref name="height"/> (the card's less its inset) holds
    /// a face <paramref name="face"/> tall at its least over the why's band, and a sentence's room across. Unmeasured: no.
    /// </summary>
    public static bool Fits(double width, double height, double face)
        => double.IsFinite(width) && double.IsFinite(height) && width >= StatusRules.MinSentence && height >= face + Band;
}
