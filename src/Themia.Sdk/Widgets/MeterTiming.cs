using Themia.Core.Localization;
using Themia.Core.SystemData;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-1121] How much history a chart spans — the one timing setting a meter has. How often it is
/// sampled is not a setting: every system figure moves on the hub's one cadence ([S-2503]).
/// <para>
/// The span is a duration, not a sample count: the number of points the chart holds follows from it
/// and the source's spacing ([S-2512]). A chart narrower than its points in slots folds neighbouring
/// points into one slot (<see cref="BucketSize"/>, <see cref="Fold"/>), so an hour at one point a
/// second is still an hour on a 300 DIP tile, not its newest two and a half minutes.
/// </para>
/// </summary>
public static class MeterTiming
{
    /// <summary>The spans the settings offer, in seconds. A closed set, so the choice row can never show a blank.</summary>
    public static IReadOnlyList<int> SpanChoices { get; } = [60, 300, 900, 1800, 3600];

    /// <summary>The longest span offered; every series' history holds this much ([S-2504]).</summary>
    public const int MaxSpanSeconds = 3600;

    /// <summary>
    /// The narrowest slot one point gets on a chart, in DIPs. Under it neighbouring points land on
    /// the same pixel column and the line is noise, not history.
    /// </summary>
    public const double MinSlotWidth = 2;

    /// <summary>
    /// How many points a <paramref name="spanSeconds"/> chart holds at one point every
    /// <paramref name="periodSeconds"/>: 2 at least, the longest span's at most.
    /// </summary>
    public static int Points(int spanSeconds, int periodSeconds)
    {
        // Junk from a hand-edited file falls back rather than reaching a division.
        var span = spanSeconds > 0 ? spanSeconds : 60;
        var period = periodSeconds > 0 ? periodSeconds : SystemCadence.TickSeconds;
        var most = Math.Max(2, MaxSpanSeconds / period);
        return Math.Clamp((int)Math.Round((double)span / period, MidpointRounding.AwayFromZero), 2, most);
    }

    /// <summary>
    /// How many neighbouring points one slot of a <paramref name="chartWidth"/> DIP chart stands for
    /// when it shows <paramref name="points"/>: 1 while every point has a <see cref="MinSlotWidth"/>
    /// slot of its own (and on an unmeasured chart), more as the chart narrows or the span grows.
    /// </summary>
    public static int BucketSize(int points, double chartWidth)
    {
        if (!double.IsFinite(chartWidth) || chartWidth <= 0 || points <= 2)
        {
            return 1;
        }

        var slots = Math.Max(2, (int)Math.Floor(chartWidth / MinSlotWidth));
        return Math.Max(1, (int)Math.Ceiling((double)points / slots));
    }

    /// <summary>How many slots a chart of <paramref name="points"/> folded by <paramref name="size"/> has.</summary>
    public static int Slots(int points, int size) => Math.Max(2, (int)Math.Ceiling((double)Math.Max(2, points) / Math.Max(1, size)));

    /// <summary>
    /// Fold <paramref name="samples"/> (oldest first; the newest is point number <paramref name="added"/>
    /// of the series) into buckets of <paramref name="size"/>, oldest first. A bucket is fixed by the
    /// points' absolute positions, not by where the newest one is, so a folded chart does not shimmer
    /// as each new point arrives: only the newest bucket changes until it is full. A
    /// <paramref name="peak"/> chart (a rate, a latency) keeps each bucket's highest point, so a burst
    /// is not averaged away; a share keeps the mean. Gaps (NaN) are skipped; an all-gap bucket is a gap.
    /// </summary>
    public static double[] Fold(ReadOnlySpan<double> samples, long added, int size, bool peak)
    {
        if (size <= 1 || samples.Length == 0)
        {
            return samples.ToArray();
        }

        var first = added - samples.Length;
        var result = new List<double>((samples.Length / size) + 2);
        long bucket = long.MinValue;
        double sum = 0;
        double top = double.NegativeInfinity;
        var count = 0;

        for (var i = 0; i < samples.Length; i++)
        {
            var id = FloorDiv(first + i, size);

            if (id != bucket)
            {
                if (bucket != long.MinValue)
                {
                    result.Add(Close(sum, top, count, peak));
                }

                bucket = id;
                sum = 0;
                top = double.NegativeInfinity;
                count = 0;
            }

            var value = samples[i];

            if (double.IsFinite(value))
            {
                sum += value;
                top = Math.Max(top, value);
                count++;
            }
        }

        result.Add(Close(sum, top, count, peak));
        return [.. result];
    }

    private static double Close(double sum, double top, int count, bool peak)
        => count == 0 ? double.NaN : peak ? top : sum / count;

    private static long FloorDiv(long a, int b) => a >= 0 ? a / b : ((a + 1) / b) - 1;

    /// <summary>The nearest offered span to a stored one, so a value outside the set still lands on a real choice.</summary>
    public static int NearestSpan(int seconds)
    {
        var best = SpanChoices[0];

        foreach (var choice in SpanChoices)
        {
            if (Math.Abs(choice - seconds) < Math.Abs(best - seconds))
            {
                best = choice;
            }
        }

        return best;
    }

    /// <summary>A span in words, for the choice row: "1 minute", "15 minutes", "1 hour".</summary>
    public static string SpanLabel(int seconds) => SpanLabel(Strings.Current, seconds);

    /// <summary><see cref="SpanLabel(int)"/> in <paramref name="locale"/>.</summary>
    public static string SpanLabel(Locale locale, int seconds) => seconds % 3600 == 0
        ? Strings.Plural(locale, Str.WidgetsB_Meters_SpanHours, seconds / 3600)
        : Strings.Plural(locale, Str.WidgetsB_Meters_SpanMinutes, (long)Math.Max(1, Math.Round(seconds / 60.0)));
}
