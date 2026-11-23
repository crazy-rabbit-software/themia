using System.Text.Json.Serialization;

namespace Themia.Sdk.Kit;

/// <summary>
/// [S-6750] How a chart draws its series (owner, 2026-11-19: "more configuration options like area like now or just
/// line graph"; "a candle stick chart type for stock widgets"): the line with its wash, the line alone, a round
/// column per slot, or a candle per period. A kind offers the types its data has (<c>IChartLooks.ChartTypes</c>).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ChartType>))]
public enum ChartType
{
    /// <summary>The line at the Chart block's stroke with its 16 % wash under it: the histories' look until now.</summary>
    Area = 0,

    /// <summary>The line alone, no wash.</summary>
    Line = 1,

    /// <summary>A round column per slot, filled from the foot (the weather graphs' rain, UV and wind).</summary>
    Bars = 2,

    /// <summary>A candle per period: open, high, low and close, rising in the accent and falling Critical.</summary>
    Candles = 3,
}

/// <summary>
/// [S-6751] How much a chart writes on itself (owner, 2026-11-19: "a new style for the graph based widgets like network
/// history that shows more details like lines"): Simple is the plain chart; Detailed adds the grid lines with their
/// values, a few times along the foot, the low, the high and the latest value marked, and a hairline at the latest.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ChartDetail>))]
public enum ChartDetail
{
    Simple = 0,

    Detailed = 1,
}

/// <summary>[S-6751] A label on a chart's axis: where it stands (a value, or a share of the width) and what it says.</summary>
public readonly record struct ChartTick(double At, string Text);

/// <summary>[S-6750] One period's open, high, low and close.</summary>
public readonly record struct Candle(double Open, double High, double Low, double Close)
{
    /// <summary>A sample that is one value: a candle with no body and no wick.</summary>
    public static Candle Point(double value) => new(value, value, value, value);

    /// <summary>Closed at or over its open: drawn in the accent, never green ([S-6706]); a fall is Critical.</summary>
    public bool Rises => Close >= Open;

    /// <summary>Every part finite.</summary>
    public bool IsFinite => double.IsFinite(Open) && double.IsFinite(High) && double.IsFinite(Low) && double.IsFinite(Close);
}

/// <summary>[S-6750] Where a candle is drawn: its body (the open to the close) and its wick (the low to the high).</summary>
public readonly record struct CandleShape(double BodyLeft, double BodyTop, double BodyWidth, double BodyHeight, double WickX, double WickTop, double WickBottom, bool Rises);

/// <summary>[S-6754] A box in a chart's plot, DIPs from the plot's top-left corner.</summary>
public readonly record struct ChartBox(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;

    /// <summary>Whether <paramref name="other"/> stands wholly inside this box.</summary>
    public bool Holds(ChartBox other)
        => other.Left >= Left - 1e-9 && other.Top >= Top - 1e-9 && other.Right <= Right + 1e-9 && other.Bottom <= Bottom + 1e-9;
}

/// <summary>
/// [S-6754] Where the one chart lays its parts (<see cref="ChartPlot.Frame"/>): the <paramref name="Plot"/>, its whole box
/// edge to edge; the <paramref name="Text"/> box everything written stands in (the plot less the family's inset); the
/// series' band from <paramref name="LineTop"/> to <paramref name="LineBottom"/> (columns stand on its foot); the top of the
/// foot's line of times or ends (<paramref name="FootLine"/>) and of the owner's parts over it (<paramref name="UnderTop"/>);
/// where a caption and the figures may start, under the owner's parts (<paramref name="CaptionTop"/>); and the span the
/// hairlines down the plot run (the moment now, the days' borders): from under the owner's parts at the top
/// (<paramref name="RulesTop"/>) to over what is written at the foot (<paramref name="RulesBottom"/>).
/// </summary>
public sealed record ChartFrame(ChartBox Plot, ChartBox Text, double LineTop, double LineBottom, double FootLine, double UnderTop, double CaptionTop, double RulesTop, double RulesBottom)
{
    /// <summary>The series' band's height, at least one DIP.</summary>
    public double Band => Math.Max(1, LineBottom - LineTop);
}

/// <summary>[S-6750] A series as it is stroked and, for an area, the outline its wash fills (empty for a line).</summary>
public sealed record ChartOutline(IReadOnlyList<ChartPoint> Line, IReadOnlyList<ChartPoint> Area)
{
    public static ChartOutline None { get; } = new([], []);
}

/// <summary>
/// [S-6750] [S-6751] The one chart's geometry, pure and tested: the stroke and its wash, the columns, the candles
/// (their periods bucketed from the samples), the value axis at nice round steps, the times along the foot and the room
/// a Detailed chart keeps for what it writes. Every graph widget draws through the SDK's one <c>Chart</c> over these.
/// </summary>
public static class ChartPlot
{
    /// <summary>[S-6750] A candle's least slot: its body (60 %) and a gap either side of it.</summary>
    public const double CandleSlot = FamilySpace.S3;

    /// <summary>[S-6750] The least samples a candle made of points holds: fewer is a dash, not a period.</summary>
    public const int PointsPerCandle = 4;

    /// <summary>[S-6751] A value label's least distance from the next: one and a half Caption lines.</summary>
    public const double ValueGap = FamilyType.CaptionLine * 1.5;

    /// <summary>[S-6751] A time label's least slot along the foot ("10:42:30" in Caption with a gap).</summary>
    public const double TimeSlot = 64;

    /// <summary>
    /// [S-6750] The line through <paramref name="points"/> and, for <see cref="ChartType.Area"/>, the outline of its
    /// wash: the line, then down to <paramref name="foot"/> under its last point and back under its first. A line has
    /// no wash; columns and candles are not outlines (none). Fewer than two points draw nothing.
    /// </summary>
    public static ChartOutline Outline(IReadOnlyList<ChartPoint> points, ChartType type, double foot)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 2 || type is ChartType.Bars or ChartType.Candles)
        {
            return ChartOutline.None;
        }

        if (type == ChartType.Line)
        {
            return new ChartOutline(points, []);
        }

        var area = new ChartPoint[points.Count + 2];
        for (var i = 0; i < points.Count; i++)
        {
            area[i] = points[i];
        }

        area[^2] = new ChartPoint(points[^1].X, foot);
        area[^1] = new ChartPoint(points[0].X, foot);
        return new ChartOutline(points, area);
    }

    /// <summary>
    /// [S-6750] The unbroken runs of a series: index ranges with a finite value in every sample. A gap breaks the line
    /// rather than being drawn through; a run of one sample is a dot.
    /// </summary>
    public static IReadOnlyList<(int First, int Last)> Runs(IReadOnlyList<ChartSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var runs = new List<(int, int)>();
        int? start = null;
        for (var i = 0; i <= samples.Count; i++)
        {
            var has = i < samples.Count && double.IsFinite(samples[i].Value);
            if (has && start is null)
            {
                start = i;
            }
            else if (!has && start is { } s)
            {
                runs.Add((s, i - 1));
                start = null;
            }
        }

        return runs;
    }

    /// <summary>
    /// [S-6753] What the chart draws as its series: the model's when it has a line to draw (two finite samples in a run),
    /// else a flat series at its <see cref="ChartModel.Baseline"/> across the plot <paramref name="width"/> wide. A chart
    /// is never empty (owner, 2026-11-19): no samples yet, a sensor that is not there, nothing in the window.
    /// </summary>
    public static IReadOnlyList<ChartSample> Drawn(ChartModel model, double width)
    {
        ArgumentNullException.ThrowIfNull(model);
        return HasLine(model.Series) ? model.Series : [new ChartSample(0, model.Baseline), new ChartSample(Math.Max(0, width), model.Baseline)];
    }

    /// <summary>
    /// [S-6754] A line's samples reaching the plot's sides (owner, 2026-11-19: "Plot edge to edge"): a sample stands for
    /// its cell, so where the side is within half a cell of the first sample (half the spacing to the next) the first
    /// value is held out to the left edge, and the last to the right edge (<paramref name="width"/>) likewise. A series
    /// still growing in from one side (a history filling) stays where its samples are; a gap at a side stays a gap.
    /// </summary>
    public static IReadOnlyList<ChartSample> ToEdges(IReadOnlyList<ChartSample> samples, double width)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count < 2)
        {
            return samples;
        }

        var (first, last) = (samples[0], samples[^1]);
        var left = first.X > 0 && double.IsFinite(first.Value) && first.X <= ((samples[1].X - first.X) / 2) + 1e-6;
        var right = last.X < width && double.IsFinite(last.Value) && width - last.X <= ((last.X - samples[^2].X) / 2) + 1e-6;
        if (!left && !right)
        {
            return samples;
        }

        var list = new List<ChartSample>(samples.Count + 2);
        if (left)
        {
            list.Add(first with { X = 0 });
        }

        list.AddRange(samples);
        if (right)
        {
            list.Add(last with { X = width });
        }

        return list;
    }

    /// <summary>[S-6753] Whether a series has a line: a run of two finite samples or more.</summary>
    public static bool HasLine(IReadOnlyList<ChartSample> samples) => Runs(samples).Any(r => r.Last > r.First);

    /// <summary>
    /// [S-6750] The role a line's piece from one sample to the next is drawn in: the worse of its two ends' (the accent,
    /// then Caution, then Critical), so a band's colour starts at the sample that reaches it.
    /// </summary>
    public static ColourRole Worse(ColourRole a, ColourRole b) => Rank(a) >= Rank(b) ? a : b;

    private static int Rank(ColourRole role) => role switch
    {
        ColourRole.Critical => 2,
        ColourRole.Caution => 1,
        _ => 0,
    };

    /// <summary>
    /// [S-16450] Column <paramref name="index"/> of <paramref name="count"/> spread across a plot <paramref name="width"/> wide
    /// edge to edge (owner, 2026-11-19: "Plot edge to edge"): the first on the left edge, the last on the right, the columns
    /// alike and 4 apart (a rain hour's columns stood 48 DIP in from the sides with 96 between them at 900 wide).
    /// </summary>
    public static (double Left, double Width) Spread(int index, int count, double width)
    {
        if (count <= 0 || !(width > 0))
        {
            return (0, 0);
        }

        var each = Math.Max(0, (width - ((count - 1) * FamilySpace.S1)) / count);
        return (index * (each + FamilySpace.S1), each);
    }

    /// <summary>[S-6750] A column's or a candle's width in its cell: its share of the cell, at least the stroke, at most a large icon's half.</summary>
    public static double BarWidth(double cell) => Math.Clamp(cell * 0.6, FamilyShape.Stroke, FamilyShape.IconLarge / 2);

    /// <summary>
    /// [S-6750] How many candles a plot <paramref name="width"/> wide draws of <paramref name="samples"/>: one per
    /// <see cref="CandleSlot"/>, never more than the samples; made of points (no real periods), never fewer than
    /// <see cref="PointsPerCandle"/> of them each, so each candle has a body and a wick to show.
    /// </summary>
    public static int CandleCount(int samples, double width, bool real = true)
        => samples <= 0 || !(width > 0)
            ? 0
            : Math.Clamp((int)Math.Floor(width / CandleSlot), 1, real ? samples : Math.Max(1, samples / PointsPerCandle));

    /// <summary>
    /// [S-6750] The samples in <paramref name="count"/> periods of equal runs, in time order: each period opens on its
    /// first sample's open, closes on its last's close and spans the lowest low to the highest high among them. A
    /// series of points (<see cref="Candle.Point"/>) so becomes the candles of its sampled values; real OHLC samples
    /// keep theirs. A sample that is not finite is left out; a period with none left is none.
    /// </summary>
    public static IReadOnlyList<Candle> Bucket(IReadOnlyList<Candle> samples, int count)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var kept = samples.Where(s => s.IsFinite).ToList();
        if (kept.Count == 0 || count <= 0)
        {
            return [];
        }

        count = Math.Min(count, kept.Count);
        var candles = new List<Candle>(count);
        for (var i = 0; i < count; i++)
        {
            var from = (int)((long)i * kept.Count / count);
            var to = (int)((long)(i + 1) * kept.Count / count);
            var high = double.MinValue;
            var low = double.MaxValue;
            for (var j = from; j < to; j++)
            {
                high = Math.Max(high, Math.Max(kept[j].High, Math.Max(kept[j].Open, kept[j].Close)));
                low = Math.Min(low, Math.Min(kept[j].Low, Math.Min(kept[j].Open, kept[j].Close)));
            }

            candles.Add(new Candle(kept[from].Open, high, low, kept[to - 1].Close));
        }

        return candles;
    }

    /// <summary>
    /// [S-6750] A candle's shape in the cell centred on <paramref name="x"/>, its values placed by <paramref name="y"/>
    /// (a value's height in the plot): the body from the open to the close (at least the stroke tall, so a period that
    /// closed where it opened still shows), the wick a hairline from the low to the high through the body's middle.
    /// </summary>
    public static CandleShape Shape(Candle candle, double x, double cell, Func<double, double> y)
    {
        ArgumentNullException.ThrowIfNull(y);
        var width = BarWidth(cell);
        var (open, close) = (y(candle.Open), y(candle.Close));
        var top = Math.Min(open, close);
        var height = Math.Abs(open - close);
        if (height < FamilyShape.Stroke)
        {
            top -= (FamilyShape.Stroke - height) / 2;
            height = FamilyShape.Stroke;
        }

        var (high, low) = (y(candle.High), y(candle.Low));
        return new CandleShape(x - (width / 2), top, width, height, x, Math.Min(high, low), Math.Max(high, low), candle.Rises);
    }

    /// <summary>
    /// [S-6751] The value axis' ticks between <paramref name="low"/> and <paramref name="high"/>: every multiple of the
    /// smallest nice step (1, 2 or 5 × 10ⁿ) that leaves at most <paramref name="most"/> of them in the range. A range
    /// that is no range (equal, inverted, not finite) has none.
    /// </summary>
    public static IReadOnlyList<double> Ticks(double low, double high, int most)
    {
        if (!double.IsFinite(low) || !double.IsFinite(high) || !(high > low) || most < 1)
        {
            return [];
        }

        var step = Step(high - low, most);
        var digits = Math.Clamp(1 - (int)Math.Floor(Math.Log10(step)), 0, 15);
        var ticks = new List<double>();
        var first = Math.Ceiling((low / step) - 1e-9);
        for (var n = first; ; n++)
        {
            var value = n * step;
            if (value > high + (step * 1e-9) || ticks.Count >= most)
            {
                break;
            }

            // A tick at -0 is 0, and a multiple of a step is rounded back onto the step's digits (0.30000000000000004 is 0.3).
            ticks.Add(Math.Round(value, digits) + 0.0);
        }

        return ticks;
    }

    /// <summary>[S-6751] <see cref="Ticks(double, double, int)"/> counted in <paramref name="unit"/> (a power of 1024 for bytes), back in the values' own.</summary>
    public static IReadOnlyList<double> Ticks(double low, double high, int most, double unit)
        => unit is > 0 and not 1 && double.IsFinite(unit) ? [.. Ticks(low / unit, high / unit, most).Select(t => t * unit)] : Ticks(low, high, most);

    /// <summary>[S-6751] The binary unit a byte count up to <paramref name="high"/> is written in (1, 1024, 1024², …): its grid steps are counted in it.</summary>
    public static double BinaryUnit(double high)
    {
        var unit = 1.0;
        while (double.IsFinite(high) && high >= unit * 1024 && unit < Math.Pow(1024, 5))
        {
            unit *= 1024;
        }

        return unit;
    }

    /// <summary>[S-6751] The smallest nice step (1, 2 or 5 × 10ⁿ) that puts at most <paramref name="most"/> ticks across <paramref name="span"/>.</summary>
    public static double Step(double span, int most)
    {
        if (!(span > 0) || !double.IsFinite(span))
        {
            return 1;
        }

        var raw = span / Math.Max(1, most - 1);
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        foreach (var factor in (ReadOnlySpan<double>)[1, 2, 5, 10])
        {
            var step = factor * magnitude;
            if (step >= raw * (1 - 1e-9))
            {
                return step;
            }
        }

        return 10 * magnitude;
    }

    /// <summary>[S-6751] How many value ticks a plot <paramref name="height"/> tall has room for: one per <see cref="ValueGap"/>, two to five.</summary>
    public static int ValueRoom(double height) => Math.Clamp((int)Math.Floor(Math.Max(0, height) / ValueGap), 2, 5);

    /// <summary>[S-6751] How many time labels a plot <paramref name="width"/> wide has room for: one per <see cref="TimeSlot"/>, two to six.</summary>
    public static int TimeRoom(double width) => Math.Clamp((int)Math.Floor(Math.Max(0, width) / TimeSlot), 2, 6);

    /// <summary>The steps a time axis may take, shortest first: seconds to a year.</summary>
    public static IReadOnlyList<TimeSpan> TimeSteps { get; } =
    [
        TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(1), TimeSpan.FromHours(2), TimeSpan.FromHours(3), TimeSpan.FromHours(6), TimeSpan.FromHours(12),
        TimeSpan.FromDays(1), TimeSpan.FromDays(2), TimeSpan.FromDays(7), TimeSpan.FromDays(14), TimeSpan.FromDays(30), TimeSpan.FromDays(91), TimeSpan.FromDays(182), TimeSpan.FromDays(365),
    ];

    /// <summary>
    /// [S-6751] The moments along a time axis from <paramref name="start"/> to <paramref name="end"/>: the shortest of
    /// <see cref="TimeSteps"/> that leaves at most <paramref name="most"/> of them, each on a whole step of the local
    /// clock in <paramref name="zone"/> (a minute at :00 seconds, an hour at :00, a day at midnight), with its step.
    /// </summary>
    public static (IReadOnlyList<DateTimeOffset> At, TimeSpan Step) Times(DateTimeOffset start, DateTimeOffset end, int most, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        if (end <= start || most < 1)
        {
            return ([], TimeSpan.Zero);
        }

        var span = end - start;
        var step = TimeSteps.FirstOrDefault(s => span / s < most) is { } found && found > TimeSpan.Zero ? found : TimeSteps[^1];
        var offset = zone.GetUtcOffset(start);
        var local = start.ToOffset(offset).DateTime;
        var ticks = step.Ticks;
        var aligned = new DateTime(((local.Ticks + ticks - 1) / ticks) * ticks, DateTimeKind.Unspecified);
        var list = new List<DateTimeOffset>();
        for (var at = aligned; list.Count <= most; at = at.Add(step))
        {
            var moment = new DateTimeOffset(at, zone.GetUtcOffset(at));
            if (moment > end)
            {
                break;
            }

            list.Add(moment);
        }

        return (list, step);
    }

    /// <summary>[S-6751] Where a moment stands across a plot from <paramref name="start"/> to <paramref name="end"/>, as a share of its width.</summary>
    public static double Share(DateTimeOffset at, DateTimeOffset start, DateTimeOffset end)
        => end > start ? (at - start) / (end - start) : 0;

    /// <summary>
    /// [S-6751] [S-6754] What a chart of <paramref name="detail"/> with nothing of its owner's keeps free over its line and
    /// under it inside its plot: Detailed, a Caption line over the highest point (the high's figure) and under the lowest
    /// (the low's), the times' line at the foot 4 under that, all of it the family's inset off the edges; a Simple chart
    /// keeps nothing: its line runs edge to edge.
    /// </summary>
    public static (double Top, double Bottom) Room(ChartDetail detail) => Keeps(new ChartModel { Detail = detail });

    /// <summary>[S-6754] What <paramref name="model"/>'s frame keeps free over its line and under it, whatever its box's height.</summary>
    public static (double Top, double Bottom) Keeps(ChartModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        const double tall = 10_000;
        var frame = Frame(model, tall, tall);
        return (frame.LineTop, tall - frame.LineBottom);
    }

    /// <summary>[S-6754] Text written on a chart keeps the family's inset off its edges, as text does on every card.</summary>
    public const double TextInset = FamilySpace.Inset;

    /// <summary>
    /// [S-6754] Where the one chart lays its parts in a box <paramref name="width"/> by <paramref name="height"/> (owner,
    /// 2026-11-19: "Plot edge to edge": the plot and its fill reach the card's edges, the labels stand inside it, over the
    /// plot). The plot is the whole box, the card's from edge to edge and from its top to its foot: what is written stands
    /// in it, the family's inset off every edge, and never pushes it inward. The series' band keeps clear of what is
    /// written at the plot's top and foot: the owner's parts over the top (<see cref="ChartModel.Over"/>) and a Caption
    /// line for the figures over the highest point (<see cref="ChartModel.PadTop"/>, Detailed's high); at the foot, the
    /// line of times (Detailed) or ends (Simple, <see cref="ChartModel.Ends"/>) at the inset, the owner's parts over it
    /// (<see cref="ChartModel.Under"/>), 4, and a Caption line for the figures under the lowest point. With nothing
    /// written at an edge the band reaches it. Columns stand on the band's foot, so no column hides under a time.
    /// </summary>
    public static ChartFrame Frame(ChartModel model, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(model);
        var (w, h) = (Math.Max(0, width), Math.Max(0, height));
        var detailed = model.Detail == ChartDetail.Detailed;
        var line = FamilyType.CaptionLine;
        var text = new ChartBox(TextInset, TextInset, Math.Max(0, w - (2 * TextInset)), Math.Max(0, h - (2 * TextInset)));

        // Over the line: the owner's parts (their inset theirs), then the figures' line; alone, the figures keep the inset.
        var over = Math.Max(0, model.Over);
        var figuresOver = Math.Max(Math.Max(0, model.PadTop), detailed ? line : 0);
        var top = over > 0 ? over + figuresOver : figuresOver > 0 ? TextInset + figuresOver : 0;

        // Under it: the line of times or ends at the inset, the owner's parts over that, 4, the figures' line.
        var writesFoot = detailed || model.Ends is not null;
        var under = Math.Max(0, model.Under);
        var footText = writesFoot ? line : 0;
        var figuresUnder = Math.Max(Math.Max(0, model.PadBottom), detailed ? line : 0);
        var foot = footText + under > 0 ? TextInset + footText + under + FamilySpace.S1 + figuresUnder
            : figuresUnder > 0 ? TextInset + figuresUnder : 0;

        var lineTop = Math.Min(top, h);
        var lineBottom = Math.Max(lineTop, h - foot);
        var footLine = Math.Max(0, h - TextInset - line);
        var rulesTop = Math.Min(over, h);
        return new ChartFrame(
            new ChartBox(0, 0, w, h),
            text,
            lineTop,
            lineBottom,
            footLine,
            Math.Max(0, (writesFoot ? footLine : h - TextInset) - under),
            Math.Max(over, TextInset),
            rulesTop,
            footText + under > 0 ? Math.Max(rulesTop, h - TextInset - footText - under - FamilySpace.S1) : h);
    }

    /// <summary>[S-6754] The left of a label <paramref name="width"/> wide centred on <paramref name="at"/>, kept inside the frame's text box.</summary>
    public static double Across(ChartFrame frame, double at, double width)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return Math.Clamp(at - (width / 2), frame.Text.Left, Math.Max(frame.Text.Left, frame.Text.Right - width));
    }

    /// <summary>
    /// [S-6754] The top of a label <paramref name="height"/> tall wanted at <paramref name="top"/>, kept inside the frame's
    /// text box and under the owner's parts over the plot.
    /// </summary>
    public static double Within(ChartFrame frame, double top, double height)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return Math.Clamp(top, frame.CaptionTop, Math.Max(frame.CaptionTop, frame.Text.Bottom - height));
    }

    /// <summary>
    /// [S-6751] A kind's sizing for its detail: Detailed is taller on every bound by what its <see cref="Room"/> needs
    /// past what the kind's Simple chart already keeps free over its line (<paramref name="ownTop"/>) and under it
    /// (<paramref name="ownBottom"/>, its own ends' line included: <see cref="Keeps"/>), so the chart's line keeps its height.
    /// </summary>
    public static WidgetSizing Sized(WidgetSizing sizing, ChartDetail detail, double ownTop, double ownBottom)
    {
        ArgumentNullException.ThrowIfNull(sizing);
        var (top, bottom) = Room(detail);
        var extra = Math.Max(0, top - ownTop) + Math.Max(0, bottom - ownBottom);
        if (!(extra > 0))
        {
            return sizing;
        }

        return sizing with
        {
            Min = sizing.Min with { Height = sizing.Min.Height + extra },
            Natural = sizing.Natural with { Height = sizing.Natural.Height + extra },
            Max = sizing.Max with { Height = sizing.Max.Height + extra },
        };
    }
}
