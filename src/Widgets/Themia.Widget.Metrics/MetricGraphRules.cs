using Themia.Sdk.Kit;

namespace Themia.Widget.Metrics;

/// <summary>[S-16313] The values a graph spans, low at its foot and high at its top.</summary>
public readonly record struct GraphRange(double Low, double High)
{
    public double Span => High - Low;
}

/// <summary>[S-16314] One bar of a graph: its left edge and width across the plot, DIPs.</summary>
public readonly record struct GraphBar(double Left, double Width)
{
    public double Middle => Left + (Width / 2);
}

/// <summary>
/// [S-16312]..[S-16315] The graph's geometry, pure: how many slots a plot draws and how they fold, the range it scales to, where
/// a slot stands across (a line from the left edge to the right, edge to edge; bars from the left edge to the right with a
/// gap between them, never at the plot's sides) and down, and which slot the pointer is over.
/// </summary>
public static class MetricGraphRules
{
    /// <summary>[S-16312] The narrowest room a line's point takes: under it neighbours fold into one (the hub's hour on a small card).</summary>
    public const double LinePitch = 2;

    /// <summary>[S-16312] The narrowest room a bar, a range or a candle takes with its gap.</summary>
    public const double BarPitch = 6;

    /// <summary>[S-16314] The gap between two bars, at most; a narrow bar's a third of its pitch.</summary>
    public const double BarGap = 2;

    /// <summary>[S-16313] The room left over the highest value and under the lowest (a graph not from zero), a share of the span.</summary>
    public const double Headroom = 0.08;

    /// <summary>
    /// [S-16314] Whether the graph draws its default, the flat line across its foot: wherever its span has a slot without a
    /// reading (none yet, a history still filling, a gap), so it never stands empty (WR-10).
    /// </summary>
    public static bool DrawsDefault(MetricSeries series)
    {
        ArgumentNullException.ThrowIfNull(series);
        return series.Slots.Any(s => s is null);
    }

    /// <summary>[S-16312] Whether a type draws a mark a slot (bars, ranges, candles) rather than a line through them.</summary>
    public static bool Marks(MetricGraphType type) => type is MetricGraphType.Bars or MetricGraphType.Range or MetricGraphType.Candles;

    /// <summary>[S-16312] The most slots a plot <paramref name="width"/> wide draws in <paramref name="type"/>: two at least.</summary>
    public static int MaxSlots(MetricGraphType type, double width)
        => !double.IsFinite(width) || width <= 0 ? 2 : Math.Max(2, (int)Math.Floor(width / Pitch(type)));

    /// <summary>
    /// [S-16312] How many of a quote's bars one candle stands for at least where the bars are its closes alone (a candle of
    /// one close is a dash): so the chart is the same candles, bodies and wicks, at every size (WR-11).
    /// </summary>
    public const int ClosesACandle = 4;

    /// <summary>
    /// [S-16312] The most slots a plot <paramref name="width"/> wide draws <paramref name="series"/> in as <paramref name="type"/>:
    /// <see cref="MaxSlots"/>, and for candles of closes alone (every candle a point) a candle a run of <see cref="ClosesACandle"/>.
    /// </summary>
    public static int MostSlots(MetricGraphType type, MetricSeries series, double width)
    {
        ArgumentNullException.ThrowIfNull(series);
        var most = MaxSlots(type, width);
        var candles = series.Slots.Where(s => s?.Candle is not null).Select(s => s!.Value.Candle!.Value).ToList();
        if (type == MetricGraphType.Candles && candles.Count > 0 && candles.All(c => c.High - c.Low < 1e-12))
        {
            most = Math.Min(most, Math.Max(2, (int)Math.Ceiling(series.Slots.Count / (double)ClosesACandle)));
        }

        return most;
    }

    /// <summary>[S-16312] The narrowest room a candle takes: its body and wick read, a quote's close-only bars folding into real candles.</summary>
    public const double CandlePitch = 10;

    private static double Pitch(MetricGraphType type) => type switch
    {
        MetricGraphType.Candles => CandlePitch,
        _ when Marks(type) => BarPitch,
        _ => LinePitch,
    };

    /// <summary>
    /// [S-16312] <paramref name="series"/> folded into at most <paramref name="most"/> slots: each run of neighbours one slot,
    /// fixed by the slots' positions from the end (the newest stands alone at the right, as the hub's history grows in from
    /// it); a run's value its mean, peak or sum, a pair's second alike, a candle the run's open, high, low and close; a run of
    /// gaps a gap. The slot of a run stands at its first member's time. Unfolded when it fits.
    /// </summary>
    public static MetricSeries Fold(MetricSeries series, int most, MetricFold fold)
    {
        ArgumentNullException.ThrowIfNull(series);
        var count = series.Slots.Count;
        if (most < 2 || count <= most)
        {
            return series;
        }

        var size = (int)Math.Ceiling(count / (double)most);
        var runs = (int)Math.Ceiling(count / (double)size);
        var slots = new MetricValue?[runs];
        var times = new DateTimeOffset[runs];

        // The runs end at the newest slot: the first run is the short one.
        for (var r = 0; r < runs; r++)
        {
            var end = count - ((runs - 1 - r) * size);
            var begin = Math.Max(0, end - size);
            times[r] = series.At(begin);
            slots[r] = Run(series.Slots, begin, end, fold);
        }

        return series with { Slots = slots, Times = times };
    }

    private static MetricValue? Run(IReadOnlyList<MetricValue?> slots, int begin, int end, MetricFold fold)
    {
        var held = new List<MetricValue>(end - begin);
        for (var i = begin; i < end; i++)
        {
            if (slots[i] is { } v)
            {
                held.Add(v);
            }
        }

        if (held.Count == 0)
        {
            return null;
        }

        double Of(IEnumerable<double> values) => fold switch
        {
            MetricFold.Peak => values.Max(),
            MetricFold.Sum => values.Sum(),
            _ => values.Average(),
        };

        var seconds = held.Where(v => v.Second is not null).Select(v => v.Second!.Value).ToList();
        var candles = held.Where(v => v.Candle is not null).Select(v => v.Candle!.Value).ToList();
        Candle? candle = candles.Count > 0
            ? new Candle(candles[0].Open, candles.Max(c => c.High), candles.Min(c => c.Low), candles[^1].Close)
            : null;
        var value = candle is { } c ? c.Close : Of(held.Select(v => v.Value));
        return new MetricValue(value, seconds.Count > 0 ? Of(seconds) : null, candle);
    }

    /// <summary>
    /// [S-16313] The range a graph scales to: from zero (an amount, a share) or its own low (a temperature, a price) to its
    /// high, with headroom over it (and under a low that is not zero); a share's top never under 100 %. A flat series, or none,
    /// stands its values in the middle of a range around them (0..1 with nothing at all).
    /// </summary>
    public static GraphRange Range(MetricSeries series, bool fromZero, bool share = false)
    {
        ArgumentNullException.ThrowIfNull(series);
        var values = series.Slots.Where(s => s is not null).Select(s => s!.Value).ToList();
        if (series.Now is { } now)
        {
            values.Add(now);
        }

        if (values.Count == 0)
        {
            return new GraphRange(0, share ? 1 : 1);
        }

        var low = values.Min(v => v.Low);
        var high = values.Max(v => v.High);
        if (fromZero)
        {
            low = Math.Min(0, low);
        }

        if (share)
        {
            return new GraphRange(Math.Min(0, low), Math.Max(1, high));
        }

        if (high - low < 1e-9)
        {
            var around = Math.Abs(high) > 1e-9 ? Math.Abs(high) * 0.1 : 1;
            return fromZero && low >= 0 ? new GraphRange(0, high + around) : new GraphRange(low - around, high + around);
        }

        var span = high - low;
        return new GraphRange(fromZero ? low : low - (span * Headroom), high + (span * Headroom));
    }

    /// <summary>[S-16314] Where a line's point of slot <paramref name="slot"/> of <paramref name="count"/> stands: the first on the left edge, the last on the right.</summary>
    public static double LineX(int slot, int count, double width)
        => count <= 1 ? 0 : width * slot / (count - 1);

    /// <summary>[S-16314] The widest a column is (a day's precipitation on a wide card), and a range's or a candle's.</summary>
    public const double ColumnCap = 24;

    public const double RangeCap = 12;

    /// <summary>[S-16314] The widest a mark of <paramref name="type"/> is.</summary>
    public static double Cap(MetricGraphType type) => type == MetricGraphType.Bars ? ColumnCap : RangeCap;

    /// <summary>
    /// [S-16314] Slot <paramref name="slot"/>'s bar: as wide as the bars sharing the width with a gap between each two make it,
    /// at most <paramref name="cap"/>; the first on the left edge, the last on the right, the rest evenly between (no room at the
    /// plot's sides, owner: "full width without space").
    /// </summary>
    public static GraphBar Bar(int slot, int count, double width, double cap = double.PositiveInfinity)
    {
        var n = Math.Max(1, count);
        var pitch = width / n;
        var gap = n == 1 ? 0 : Math.Min(BarGap, pitch / 3);
        var bar = Math.Max(0, Math.Min(cap, (width - (gap * (n - 1))) / n));
        var step = n == 1 ? 0 : (width - bar) / (n - 1);
        return new GraphBar(slot * step, bar);
    }

    /// <summary>[S-16314] The x of slot <paramref name="slot"/>'s mark in <paramref name="type"/>: a line's point, a bar's middle.</summary>
    public static double X(MetricGraphType type, int slot, int count, double width)
        => Marks(type) ? Bar(slot, count, width, Cap(type)).Middle : LineX(slot, count, width);

    /// <summary>[S-16315] Where <paramref name="value"/> stands down a band from <paramref name="top"/> to <paramref name="foot"/>: the range's low on the foot.</summary>
    public static double Y(double value, GraphRange range, double top, double foot)
    {
        if (!(range.Span > 0))
        {
            return (top + foot) / 2;
        }

        var share = Math.Clamp((value - range.Low) / range.Span, 0, 1);
        return foot - (share * (foot - top));
    }

    /// <summary>
    /// [S-16315] The slot under the pointer at <paramref name="x"/> across a plot <paramref name="width"/> wide: a line's
    /// nearest point, a bar's own slot (its gap the nearer bar's); null off the plot or with nothing drawn.
    /// </summary>
    public static int? SlotAt(MetricGraphType type, double x, int count, double width)
    {
        if (count <= 0 || !(width > 0) || !double.IsFinite(x) || x < 0 || x > width)
        {
            return null;
        }

        if (Marks(type))
        {
            // The nearest mark's middle.
            var first = Bar(0, count, width, Cap(type));
            var step = count == 1 ? 1 : Bar(1, count, width, Cap(type)).Left;
            return Math.Clamp((int)Math.Round((x - first.Middle) / step), 0, count - 1);
        }

        return Math.Clamp((int)Math.Round(x / width * (count - 1)), 0, count - 1);
    }

    /// <summary>
    /// [S-16315] The reading the pointer stands on: the slot under it or, where that slot is a gap, the nearest slot with a
    /// reading (a line runs on past a gap's edge); null with no reading at all.
    /// </summary>
    public static int? Nearest(IReadOnlyList<MetricValue?> slots, int slot)
    {
        ArgumentNullException.ThrowIfNull(slots);
        for (var d = 0; d < slots.Count; d++)
        {
            if (slot - d >= 0 && slot - d < slots.Count && slots[slot - d] is not null)
            {
                return slot - d;
            }

            if (slot + d < slots.Count && slot + d >= 0 && slots[slot + d] is not null)
            {
                return slot + d;
            }
        }

        return null;
    }
}
