using Themia.Sdk.Kit;

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
/// <param name="Low">The lows as a second line, under the highs.</param>
/// <param name="Precipitation">The precipitation under each column.</param>
/// <param name="ChartHeight">The chart's band: the lines and the figures over and under them.</param>
public sealed record ForecastCurvePlan(int Days, double ColumnWidth, bool Icon, bool Low, bool Precipitation, double ChartHeight)
{
    /// <summary>Where the warmest point sits in the band: under its figure's Caption line, the stroke kept inside.</summary>
    public double Top => FamilyType.Caption.LineHeight + (FamilyShape.Stroke / 2);

    /// <summary>Where the coldest point sits: over the lows' figures when they are drawn, the stroke kept inside.</summary>
    public double Bottom => ChartHeight - (Low ? FamilyType.Caption.LineHeight : 0) - (FamilyShape.Stroke / 2);
}

/// <summary>
/// [S-6164] The forecast's temperature curve: each day's high (and, with the height, its low) as a
/// point on its column's middle, against the range of the days shown, joined by a smooth line that
/// never swings past a day's own value, drawn by the Chart block ([S-6717]). Pure geometry, tested;
/// the view only draws it.
/// </summary>
public static class ForecastCurve
{
    /// <summary>The narrowest column that holds "Today" and "-18°".</summary>
    public const double ColumnMin = 48;

    /// <summary>The line's own band at its smallest, without the figures over and under it.</summary>
    public const double CurveMin = 24;

    /// <summary>What the low line adds: its figures' line and the family's gap between the two lines.</summary>
    public static double LowCost => FamilyType.Caption.LineHeight + FamilySpace.S2;

    /// <summary>Between two parts of a column.</summary>
    public const double Gap = FamilySpace.S1;

    /// <summary>Each smooth piece is drawn as this many straight steps (the Chart block draws polylines).</summary>
    public const int StepsPerDay = 12;

    /// <summary>
    /// The ladder: the day labels and the high line with its figures always; then the glyph, the low
    /// line, the precipitation; the chart takes what they leave. As many days as have
    /// <see cref="ColumnMin"/> each, never more than the forecast has: no scrolling, no squeezing.
    /// </summary>
    public static ForecastCurvePlan Plan(WidgetArea area, int days, bool precipitation)
    {
        days = Math.Max(1, days);
        var width = WeatherForecastLayout.ContentWidth(area);
        var height = WeatherForecastLayout.ContentHeight(area);
        var shown = Math.Clamp((int)Math.Floor((width + Disclosure.Tolerance) / ColumnMin), 1, days);
        var column = width / shown;
        var label = FamilyType.Body.LineHeight;
        var caption = FamilyType.Caption.LineHeight;
        var room = height - label - Gap - (caption + CurveMin);

        bool Take(double cost)
        {
            if (room + Disclosure.Tolerance < cost)
            {
                return false;
            }

            room -= cost;
            return true;
        }

        var icon = Take(FamilyShape.Icon + Gap);
        var low = Take(LowCost);
        var rain = precipitation && Take(caption + Gap);
        var chart = Math.Max(caption + CurveMin, height - label - Gap - (icon ? FamilyShape.Icon + Gap : 0) - (rain ? caption + Gap : 0));
        return new ForecastCurvePlan(shown, column, icon, low, rain, chart);
    }

    /// <summary>
    /// The smooth line of a run as straight steps for the Chart block: its start, then
    /// <paramref name="perSegment"/> points along each piece, ending on every day's own point.
    /// </summary>
    public static IReadOnlyList<CurvePoint> Sample(CurveRun run, int perSegment = StepsPerDay)
    {
        ArgumentNullException.ThrowIfNull(run);
        perSegment = Math.Max(1, perSegment);
        var points = new List<CurvePoint>((run.Segments.Count * perSegment) + 1) { run.Start };
        var from = run.Start;

        foreach (var segment in run.Segments)
        {
            for (var k = 1; k <= perSegment; k++)
            {
                var t = (double)k / perSegment;
                var u = 1 - t;
                var x = (u * u * u * from.X) + (3 * u * u * t * segment.Control1.X) + (3 * u * t * t * segment.Control2.X) + (t * t * t * segment.End.X);
                var y = (u * u * u * from.Y) + (3 * u * u * t * segment.Control1.Y) + (3 * u * t * t * segment.Control2.Y) + (t * t * t * segment.End.Y);
                points.Add(k == perSegment ? segment.End : new CurvePoint(x, y));
            }

            from = segment.End;
        }

        return points;
    }

    /// <summary>
    /// The range the curves share: the highs' and the lows' together (or the highs' alone without a
    /// low curve), and the temperature now when it is marked ([S-6172]), so the now mark is always on
    /// the chart rather than pinned to its edge. Nothing usable is the range 0..1.
    /// </summary>
    public static (double Coldest, double Warmest) Scale(IEnumerable<double?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var coldest = double.MaxValue;
        var warmest = double.MinValue;

        foreach (var value in values)
        {
            if (value is { } v && double.IsFinite(v))
            {
                coldest = Math.Min(coldest, v);
                warmest = Math.Max(warmest, v);
            }
        }

        return coldest <= warmest ? (coldest, warmest) : (0, 1);
    }

    /// <summary>Down the chart: the warmest at <paramref name="top"/>, the coldest at <paramref name="bottom"/>, one temperature in the middle.</summary>
    public static double Y(double value, double coldest, double warmest, double top, double bottom)
    {
        var span = warmest - coldest;
        var share = span > 0 && double.IsFinite(span) ? Math.Clamp((warmest - value) / span, 0, 1) : 0.5;
        return top + (share * (bottom - top));
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

        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is not { } v || !double.IsFinite(v))
            {
                continue;
            }

            points[i] = new CurvePoint((i + 0.5) * columnWidth, Y(v, coldest, warmest, top, bottom));
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
