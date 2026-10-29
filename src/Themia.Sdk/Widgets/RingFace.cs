using Themia.Core.Geometry;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-6263] The ping style's signal bars, as Windows draws a Wi-Fi signal: four bars rising left to
/// right, as many lit as the latency deserves.
/// </summary>
public static class PingSignal
{
    public const int BarCount = 4;

    /// <summary>
    /// The latencies, in milliseconds, at or under which an answer earns 4, 3 and 2 bars: up to 50 ms is
    /// as good as a connection gets for a call or a game, up to 100 ms is fine for anything but a
    /// fast game, up to 200 ms is felt in a call; past that one bar. An unanswered probe is none.
    /// </summary>
    public static readonly IReadOnlyList<double> Thresholds = [50, 100, 200];

    /// <summary>
    /// How many bars a probe earns: none for an unanswered one (null, NaN, negative), else 4 down
    /// to 1 by <see cref="Thresholds"/>.
    /// </summary>
    public static int Level(double? latencyMs)
    {
        if (latencyMs is not { } ms || double.IsNaN(ms) || ms < 0)
        {
            return 0;
        }

        var bars = BarCount;
        foreach (var threshold in Thresholds)
        {
            if (ms <= threshold)
            {
                return bars;
            }

            bars--;
        }

        return bars;
    }
}

/// <summary>[S-6266] A ring and the lines that go with it: beside or under, the ring's diameter and how many lines fit.</summary>
public readonly record struct RingFacePlan(bool Beside, double Diameter, int Lines);

/// <summary>
/// [S-6266] A ring style's face (the battery's charge, a drive's used share): the ring is the
/// must-show part and takes what is left; the lines (a drive's name, its space) come in priority
/// order where they fit, beside the ring on a wide strip, under it otherwise.
/// </summary>
public static class RingFace
{
    /// <summary>The least ring worth drawing: under it the figure inside no longer fits.</summary>
    public const double MinRing = 48;

    /// <summary>Beside the ring, the lines' column needs at least this width.</summary>
    public const double MinColumn = 120;

    public const double Gap = 12;

    public static RingFacePlan Plan(WidgetArea box, bool wide, IReadOnlyList<double> lineHeights, double lineGap)
    {
        ArgumentNullException.ThrowIfNull(lineHeights);
        var width = Math.Max(0, box.Width);
        var height = Math.Max(0, box.Height);

        if (wide && lineHeights.Count > 0)
        {
            var diameter = Math.Floor(Math.Min(height, width - MinColumn - Gap));
            if (diameter >= MinRing)
            {
                return new RingFacePlan(true, diameter, Lines(lineHeights, height, lineGap).Count);
            }
        }

        var (shown, spent) = Lines(lineHeights, height - MinRing - Gap, lineGap);
        spent += shown > 0 ? Gap : 0;
        return new RingFacePlan(false, Math.Floor(Math.Max(0, Math.Min(width, height - spent))), shown);
    }

    /// <summary>How many of the lines, in order, fit a height, and the height they take with their gaps.</summary>
    private static (int Count, double Height) Lines(IReadOnlyList<double> lineHeights, double room, double gap)
    {
        var used = 0.0;
        var count = 0;

        foreach (var line in lineHeights)
        {
            var cost = line + (count > 0 ? gap : 0);
            if (used + cost > room + Disclosure.Tolerance)
            {
                break;
            }

            used += cost;
            count++;
        }

        return (count, used);
    }

    /// <summary>
    /// The figure inside a ring of <paramref name="diameter"/> (its stroke <see cref="RingArc.Stroke"/>):
    /// the largest step from Caption whose line fits a chord through the hole, clear of the stroke.
    /// </summary>
    public static TypeStep Figure(double diameter, HeroBound bound)
    {
        var hole = Hole(diameter);
        return HeroLayout.Fit(hole, hole * 0.5, bound.Value, bound.Unit, FluentType.Caption, FluentType.Display);
    }

    /// <summary>The width a line inside a ring may take: the hole, a little in from the stroke.</summary>
    public static double Hole(double diameter)
        => Math.Floor(Math.Max(0, diameter - (2 * RingArc.Stroke(diameter))) * 0.86);
}
