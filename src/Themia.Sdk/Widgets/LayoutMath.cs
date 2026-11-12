namespace Themia.Core.Widgets;

/// <summary>
/// [S-662] The arithmetic a view measures its fixed parts with: one rounding tolerance, and whole lines
/// counted the same way everywhere (a timed event's lines in its block, a scroll's last line, a page of
/// rows). It never decides what a widget shows: that is its config ([S-11800]).
/// </summary>
public static class LayoutMath
{
    /// <summary>Layout rounding: a part that is short by a fraction of a pixel still fits.</summary>
    public const double Tolerance = 0.5;

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
