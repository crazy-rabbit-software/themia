namespace Themia.Core.Widgets;

/// <summary>
/// [S-662] The arithmetic a view measures its fixed parts with: one rounding token (a device pixel, <see cref="Pixel"/>, and
/// what pure sizing takes of it at 100 %, where a pixel is largest: <see cref="Tolerance"/>, <see cref="Slack"/>), and whole
/// lines counted the same way everywhere (a timed event's lines in its block, a scroll's last line, a page of rows). It
/// never decides what a widget shows: that is its config ([S-11800]).
/// </summary>
public static class LayoutMath
{
    /// <summary>A device pixel at 100 %, in DIP: the largest a pixel is, what a rule with no display rounds by.</summary>
    public const double Pixel100 = 1;

    /// <summary>The one rounding token: a device pixel at <paramref name="raster"/> (the display's scale), in DIP; none known, at 100 %.</summary>
    public static double Pixel(double raster) => raster > 0 && double.IsFinite(raster) ? 1 / raster : Pixel100;

    /// <summary>Layout rounding: a part that is short by under half a pixel still fits.</summary>
    public const double Tolerance = Pixel100 / 2;

    /// <summary>
    /// The room a sized face is given past its own size (a clock's, a timer's, a pomodoro's), so a box rounded to the
    /// device's pixels still holds it: a pixel on either side.
    /// </summary>
    public const double Slack = 2 * Pixel100;

    /// <summary>
    /// How many whole lines of <paramref name="lineHeight"/>, <paramref name="gap"/> apart, fit in
    /// <paramref name="height"/>. Zero for a box too short for one, or one not measured yet.
    /// </summary>
    public static int WholeLines(double height, double lineHeight, double gap = 0)
    {
        if (!double.IsFinite(height) || !double.IsFinite(lineHeight) || lineHeight <= 0 || height <= 0)
        {
            return 0;
        }

        var step = lineHeight + Math.Max(0, double.IsFinite(gap) ? gap : 0);
        var lines = Math.Floor((height + Math.Max(0, step - lineHeight) + Tolerance) / step);
        return lines >= int.MaxValue ? int.MaxValue : (int)lines;
    }
}
