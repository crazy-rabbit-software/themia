namespace Themia.Core.Widgets;

/// <summary>A point of the forecast curve, in DIPs from the chart's top-left corner.</summary>
public readonly record struct CurvePoint(double X, double Y);

/// <summary>One cubic Bézier piece of a curve: from the previous piece's end (or the run's start) to <see cref="End"/>.</summary>
public readonly record struct CurveSegment(CurvePoint Control1, CurvePoint Control2, CurvePoint End);

/// <summary>
/// An unbroken stretch of the curve: the days in a row that have a value. A day without one breaks
/// the curve rather than being drawn through ([S-6164]); a run of one day is a dot.
/// </summary>
/// <param name="FirstDay">The index of the run's first day.</param>
/// <param name="Start">Where it starts.</param>
/// <param name="Segments">The pieces after <see cref="Start"/>, one per following day.</param>
public sealed record CurveRun(int FirstDay, CurvePoint Start, IReadOnlyList<CurveSegment> Segments);

/// <summary>What the temperature-curve style draws at one size ([S-6164]).</summary>
/// <param name="Days">How many days are shown: the first ones, as many as have a column's width.</param>
/// <param name="ColumnWidth">Each day's column; a day's point sits on its middle.</param>
/// <param name="Icon">The sky's glyph under each day's label.</param>
/// <param name="Low">The lows as a second curve, under the highs.</param>
/// <param name="Precipitation">The precipitation under each column.</param>
/// <param name="ChartHeight">The chart's band: the curves and the figures on them.</param>
public sealed record ForecastCurvePlan(int Days, double ColumnWidth, bool Icon, bool Low, bool Precipitation, double ChartHeight);

/// <summary>
/// [S-6164] The forecast's temperature curve: each day's high (and, with the height, its low) as a
/// point on its column's middle, against the range of the days shown, joined by a smooth curve that
/// never swings past a day's own value. Pure geometry, tested; the view only draws it.
/// </summary>
public static class ForecastCurve
{
    /// <summary>The style's own inset on every side.</summary>
    public const double Padding = 8;

    /// <summary>The narrowest column that holds "Today" and "-18°" in Caption.</summary>
    public const double ColumnMin = 48;

    /// <summary>The curve's own band at its smallest, without the figures over and under it.</summary>
    public const double CurveMin = 24;

    /// <summary>What the low curve adds: its figures' line and a band between the two curves.</summary>
    public static double LowCost => FluentType.Caption.LineHeight + 16;

    /// <summary>Between two parts of a column.</summary>
    public const double Gap = 4;

    /// <summary>The sky's glyph.</summary>
    public const double IconSize = 20;

    /// <summary>
    /// The ladder: the day labels and the high curve with its figures always; then the glyph, the
    /// low curve, the precipitation; the chart takes what they leave. As many days as have
    /// <see cref="ColumnMin"/> each, never more than the forecast has: no scrolling, no squeezing.
    /// </summary>
    public static ForecastCurvePlan Plan(WidgetArea slot, int days, bool precipitation)
    {
        days = Math.Max(1, days);
        var width = Math.Max(0, slot.Width - (2 * Padding));
        var shown = Math.Clamp((int)Math.Floor(width / ColumnMin), 1, days);
        var column = width / shown;
        var label = FluentType.Caption.LineHeight;

        var room = new Disclosure(column, Math.Max(0, slot.Height - (2 * Padding)), Gap);
        room.Reserve(label);
        room.Reserve(label + CurveMin);
        var icon = room.Take(IconSize);
        var low = room.Take(LowCost);
        var rain = precipitation && room.Take(label);

        // The chart is the column less the rows over and under it (one gap between two rows); the
        // curves' own band is inside it, so the ladder's parts for it are not rows of their own.
        var chart = Math.Max(label + CurveMin, slot.Height - (2 * Padding) - label - Gap
            - (icon ? IconSize + Gap : 0) - (rain ? label + Gap : 0));
        return new ForecastCurvePlan(shown, column, icon, low, rain, chart);
    }

    /// <summary>
    /// Where each day's value sits: across, its column's middle; down, its place between the warmest
    /// (<paramref name="top"/>) and the coldest (<paramref name="bottom"/>) of the range. A range of
    /// one temperature puts every point in the middle. A missing or unusable value is null.
    /// </summary>
    public static IReadOnlyList<CurvePoint?> Points(
        IReadOnlyList<double?> values, double coldest, double warmest, double columnWidth, double top, double bottom)
    {
        ArgumentNullException.ThrowIfNull(values);
        var points = new CurvePoint?[values.Count];
        var span = warmest - coldest;

        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is not { } v || !double.IsFinite(v))
            {
                continue;
            }

            var share = span > 0 && double.IsFinite(span) ? Math.Clamp((warmest - v) / span, 0, 1) : 0.5;
            points[i] = new CurvePoint((i + 0.5) * columnWidth, top + (share * (bottom - top)));
        }

        return points;
    }

    /// <summary>
    /// The smooth curve through <paramref name="points"/>: a monotone cubic (Fritsch–Carlson), so
    /// between two days it rises or falls without overshooting either: the top of the curve is a
    /// day's high, never a bump the forecast did not say. A null point breaks it into runs.
    /// </summary>
    public static IReadOnlyList<CurveRun> Smooth(IReadOnlyList<CurvePoint?> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var runs = new List<CurveRun>();
        var i = 0;

        while (i < points.Count)
        {
            if (points[i] is null)
            {
                i++;
                continue;
            }

            var first = i;
            var run = new List<CurvePoint>();
            while (i < points.Count && points[i] is { } p)
            {
                run.Add(p);
                i++;
            }

            runs.Add(new CurveRun(first, run[0], Segments(run)));
        }

        return runs;
    }

    private static List<CurveSegment> Segments(List<CurvePoint> p)
    {
        var n = p.Count;
        var segments = new List<CurveSegment>(Math.Max(0, n - 1));
        if (n < 2)
        {
            return segments;
        }

        // Secant slopes, then tangents: the mean of the neighbours where they agree in sign, flat
        // at a turn, limited so no piece leaves the box its two ends span.
        var slope = new double[n - 1];
        for (var k = 0; k < n - 1; k++)
        {
            var dx = p[k + 1].X - p[k].X;
            slope[k] = dx > 0 ? (p[k + 1].Y - p[k].Y) / dx : 0;
        }

        var tangent = new double[n];
        tangent[0] = slope[0];
        tangent[n - 1] = slope[n - 2];
        for (var k = 1; k < n - 1; k++)
        {
            tangent[k] = slope[k - 1] * slope[k] <= 0 ? 0 : (slope[k - 1] + slope[k]) / 2;
        }

        for (var k = 0; k < n - 1; k++)
        {
            if (slope[k] == 0)
            {
                tangent[k] = 0;
                tangent[k + 1] = 0;
                continue;
            }

            var a = tangent[k] / slope[k];
            var b = tangent[k + 1] / slope[k];
            var h = (a * a) + (b * b);
            if (h > 9)
            {
                var t = 3 / Math.Sqrt(h);
                tangent[k] = t * a * slope[k];
                tangent[k + 1] = t * b * slope[k];
            }
        }

        for (var k = 0; k < n - 1; k++)
        {
            var third = (p[k + 1].X - p[k].X) / 3;
            segments.Add(new CurveSegment(
                new CurvePoint(p[k].X + third, p[k].Y + (tangent[k] * third)),
                new CurvePoint(p[k + 1].X - third, p[k + 1].Y - (tangent[k + 1] * third)),
                p[k + 1]));
        }

        return segments;
    }
}
