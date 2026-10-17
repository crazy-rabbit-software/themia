namespace Themia.Core.Astronomy;

/// <summary>
/// The lit limb of the moon, as a closed outline. [S-13]
/// <para>
/// Two arcs joined: the outer limb on the lit side, and the terminator — an ellipse whose half-width
/// is <c>r·(1 − 2k)</c> for an illuminated fraction <c>k</c>. Positive for a crescent, so it bows
/// toward the lit edge; zero at quarter, a straight line; negative for a gibbous, so it bows past
/// the centre.
/// </para>
/// <para>
/// One function for both the badge on the graphic and the glyph in the row list, so the two can
/// never disagree about how much of the moon is lit.
/// </para>
/// </summary>
public static class MoonDisc
{
    /// <summary>
    /// The outline of the lit part of a disc of radius <paramref name="radius"/> centred on
    /// (<paramref name="centreX"/>, <paramref name="centreY"/>).
    /// </summary>
    /// <param name="phase">0..1 from new to new; 0.5 is full.</param>
    /// <param name="southern">
    /// Mirror the disc. A waxing moon is lit on the right seen from Zurich and on the left seen from
    /// Sydney, so a southern observer gets the same phase the other way round.
    /// </param>
    /// <param name="samples">Points per arc. The default reads as a smooth circle at any size a widget uses.</param>
    public static IReadOnlyList<PlotPoint> LitLimb(
        double centreX,
        double centreY,
        double radius,
        double phase,
        bool southern = false,
        int samples = 48)
    {
        var steps = Math.Max(2, samples);
        var wrapped = ((phase % 1) + 1) % 1;
        var illuminated = (1 - Math.Cos(2 * Math.PI * wrapped)) / 2;

        // Which side the light is on: right while waxing, left while waning, mirrored below the
        // equator.
        var side = (wrapped < 0.5 ? 1 : -1) * (southern ? -1 : 1);
        var terminatorHalfWidth = radius * (1 - (2 * illuminated));

        var points = new PlotPoint[(steps + 1) * 2];

        for (var i = 0; i <= steps; i++)
        {
            var theta = Math.PI * i / steps; // 0 = top, pi = bottom
            points[i] = new PlotPoint(
                centreX + (side * radius * Math.Sin(theta)),
                centreY - (radius * Math.Cos(theta)));
        }

        for (var i = 0; i <= steps; i++)
        {
            var theta = Math.PI * (steps - i) / steps;
            points[steps + 1 + i] = new PlotPoint(
                centreX + (side * terminatorHalfWidth * Math.Sin(theta)),
                centreY - (radius * Math.Cos(theta)));
        }

        return points;
    }
}
