using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-14603] What a single-metric widget draws with its value (owner, 2026-11-20: "you should also have the option to
/// show trendlines as well or just the trend with arrows"): nothing, an arrow beside the value (up, down or flat against
/// the reading before), or a trend line (the metric's own history on the meters' history band behind the row, [S-14220]).
/// A widget's setting, None by default.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<MetricTrend>))]
public enum MetricTrend
{
    None,
    Arrow,
    Line,
}

/// <summary>[S-14603] Which way a metric went since the reading before.</summary>
public enum TrendDirection
{
    Flat,
    Up,
    Down,
}

/// <summary>
/// [S-14603] What history a metric has, so what trend it can draw: none (a text, a constant or a time: no trend line
/// on its menu), its readings only (the last two the widget saw: an arrow), or a series (the hub's charted series, the
/// service's hours: an arrow and a line).
/// </summary>
public enum MetricHistory
{
    None,
    Readings,
    Series,
}

/// <summary>
/// [S-14601] One metric as drawn: its text with its unit ("1,013 hPa"; <see cref="MetricRules.Default"/> without a
/// reading), the number a trend compares (null for a text: an address, a name), and whether it can be read now (the
/// value drawn unavailable when not).
/// </summary>
public readonly record struct MetricValue(string Text, double? Number = null, bool Available = true)
{
    /// <summary>[S-14602] No reading: "--", unavailable.</summary>
    public static MetricValue Unread { get; } = new(MetricRules.Default, null, false);
}

/// <summary>
/// [S-14601] What one read of a single-metric widget puts on its row: the value, the reading it belongs to (a new
/// stamp is a new reading, so a redraw of the same one is never a move), the direction where the source knows it (this
/// hour against the last), the history a line draws, the problems its status glyph says (a stale reading) and why
/// there is no value (the tooltip over "--").
/// </summary>
public sealed record MetricShown(MetricValue Value)
{
    /// <summary>[S-14603] Which reading this is: the same stamp twice is one reading drawn again.</summary>
    public long Reading { get; init; }

    /// <summary>[S-14603] Which way it went, where the source knows (this hour against the last); null: against the widget's own last reading.</summary>
    public TrendDirection? Direction { get; init; }

    /// <summary>[S-14603] The metric's history, oldest first, for its line (null: none; a gap is null).</summary>
    public IReadOnlyList<double?>? Series { get; init; }

    /// <summary>[S-14602] The status glyph's problems (a reading kept through a failed refresh).</summary>
    public IReadOnlyList<string> Problems { get; init; } = [];

    /// <summary>[S-14602] Why there is no value: the tooltip over "--".</summary>
    public string? Why { get; init; }

    /// <summary>[S-14604] The reading's own name where its source says it more exactly ("Full in" while charging); null: the kind's.</summary>
    public string? Name { get; init; }
}

/// <summary>[S-14603] A config whose metric can draw a trend: the setting and what the kind's history allows.</summary>
public interface IMetricTrend
{
    /// <summary>[S-14603] The trend it draws (None, Arrow, Line).</summary>
    MetricTrend Trend { get; set; }

    /// <summary>[S-14603] What history its kind's metric has: which trends are offered.</summary>
    [JsonIgnore]
    MetricHistory History { get; }
}

/// <summary>
/// [S-14600]..[S-14605] The single-metric widget's rules, pure, one base for every one-reading widget (owner,
/// 2026-11-20: "split all the other widgets that are alike to the weather and air facts into individual metrics too.
/// use the same base component for all of them"): its size (one fact row, whole grid cells, the card hugging it), the
/// default without a reading, the trends a history allows, the arrow's direction and the trend line's chart.
/// </summary>
public static class MetricRules
{
    /// <summary>[S-14602] The value without a reading.</summary>
    public const string Default = "--";

    /// <summary>[S-14600] The narrowest card: a short name and a value side by side.</summary>
    public const double MinWidth = 200;

    /// <summary>[S-14600] A new card's width.</summary>
    public const double NaturalWidth = 240;

    /// <summary>[S-14600] The widest the content asks for (relaxed past it, [S-12411]).</summary>
    public const double MaxWidth = 420;

    /// <summary>[S-14603] The most points a trend line draws: the newest of its history.</summary>
    public const int LinePoints = 24;

    /// <summary>
    /// [S-14600] The one row's height: a 28 fact row in whole grid cells (WR-6), three on the default grid, from the card's
    /// top edge to its foot edge, so the card's top and bottom spacing is a list row's.
    /// </summary>
    public static double RowHeight => ItemGrid.Default().Whole(FamilyShape.Row1);

    /// <summary>
    /// [S-14600] [S-14100] The height's one step: the card is its one row (owner, 2026-11-20: "the individual weather facts
    /// like pressure have a way to high height. the contents should also fit to the height"), never taller, so it hugs it;
    /// the row whole grid cells of the card's own grid from its top edge to its foot edge (<see cref="SizeSteps.InCells"/>),
    /// so on any pitch the card is exactly its row, no cell left over.
    /// </summary>
    public static SizeSteps HeightSteps { get; } = SizeSteps.InCells(FamilyShape.Row1, 1, 1);

    /// <summary>[S-14600] The sizing of a single metric drawing no trend: 200..420 wide (relaxed), one row tall.</summary>
    public static WidgetSizing Sizing { get; } = SizingFor(MetricTrend.None);

    /// <summary>
    /// [S-14600] [S-14603] The sizing of a single metric drawing <paramref name="trend"/>: its one row tall; 200..420 wide
    /// and, beside the value, the arrow's room with its gap, so the name and the value keep theirs (content decides the
    /// size, [S-2445]); a line takes none, it is the band behind the row ([S-14220]).
    /// </summary>
    public static WidgetSizing SizingFor(MetricTrend trend)
    {
        var room = TrendRoom(trend);
        return WidgetSizing.Range(new(MinWidth + room, RowHeight), new(NaturalWidth + room, RowHeight), new(MaxWidth + room, RowHeight))
            .WithHeightSteps(HeightSteps);
    }

    /// <summary>
    /// [S-14603] The width a trend takes beside the value: the arrow its own and the gap before the value; a line none, it
    /// runs behind the whole row ([S-14220]).
    /// </summary>
    public static double TrendRoom(MetricTrend trend) => trend switch
    {
        MetricTrend.Arrow => FamilyShape.Icon + FamilySpace.ColumnGap,
        _ => 0,
    };

    /// <summary>[S-14600] The sizing of <paramref name="config"/>: by the trend it draws (none for a config without one).</summary>
    public static WidgetSizing SizingOf(WidgetConfig config)
        => SizingFor(config is IMetricTrend trend ? Effective(trend) : MetricTrend.None);

    /// <summary>[S-14603] The trends <paramref name="history"/> allows, in the menu's order: none for a metric without one.</summary>
    public static IReadOnlyList<MetricTrend> Offered(MetricHistory history) => history switch
    {
        MetricHistory.Series => [MetricTrend.None, MetricTrend.Arrow, MetricTrend.Line],
        MetricHistory.Readings => [MetricTrend.None, MetricTrend.Arrow],
        _ => [],
    };

    /// <summary>[S-14603] The trend drawn: the one asked when offered; a line without a series is the arrow; else none.</summary>
    public static MetricTrend Effective(MetricTrend wanted, MetricHistory history)
    {
        var offered = Offered(history);
        return offered.Contains(wanted) ? wanted
            : wanted == MetricTrend.Line && offered.Contains(MetricTrend.Arrow) ? MetricTrend.Arrow
            : MetricTrend.None;
    }

    /// <inheritdoc cref="Effective(MetricTrend, MetricHistory)"/>
    public static MetricTrend Effective(IMetricTrend config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return Effective(config.Trend, config.History);
    }

    /// <summary>[S-14603] A stored trend the kind cannot draw is the one it can, loudly; null when it holds.</summary>
    public static string? Repair(IMetricTrend config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var effective = Effective(config);
        if (!Enum.IsDefined(config.Trend) || effective != config.Trend)
        {
            var note = $"{config.GetType().Name}: trend {config.Trend} is not offered; drawing {effective}";
            config.Trend = effective;
            return note;
        }

        return null;
    }

    /// <summary>
    /// [S-14603] Which way the shown figure went from <paramref name="previous"/> to <paramref name="current"/>: flat while
    /// its text is the same (the arrow says what the figure shown did, never a move under its precision), else up or down
    /// by the numbers; null where either has no number (no reading, a text) or no reading came before.
    /// </summary>
    public static TrendDirection? Direction(MetricValue? previous, MetricValue current)
    {
        if (previous is not { Number: { } before } prior || current.Number is not { } now || !double.IsFinite(before) || !double.IsFinite(now))
        {
            return null;
        }

        if (string.Equals(prior.Text, current.Text, StringComparison.Ordinal) || now == before)
        {
            return TrendDirection.Flat;
        }

        return now > before ? TrendDirection.Up : TrendDirection.Down;
    }

    /// <summary>
    /// [S-14220] The trend line's points on the meters' history band (<c>SparkBand</c>, a share scale): the newest
    /// <see cref="LinePoints"/> values of <paramref name="series"/>, oldest first, each as its share of their own range (the
    /// low at the band's foot, the high at its top; a flat history a level line through the middle). A gap is left out,
    /// as the band leaves out a sample not read; with fewer than two values the band draws its baseline, never nothing
    /// (WR-10).
    /// </summary>
    public static IReadOnlyList<SeriesPoint> Band(IReadOnlyList<double?>? series)
    {
        var values = (series ?? []).TakeLast(LinePoints).Where(v => v is { } x && double.IsFinite(x)).Select(v => v!.Value).ToList();
        if (values.Count == 0)
        {
            return [];
        }

        var low = values.Min();
        var span = values.Max() - low;
        return [.. values.Select(v => new SeriesPoint(span > 1e-9 ? (v - low) / span : 0.5))];
    }

    /// <summary>
    /// [S-14603] A metric's trend over a service's hours (the weather's, the air's; "previous hour"): the hour under way
    /// at <paramref name="now"/> (the last that began by then) against the one before, both rounded to
    /// <paramref name="digits"/> as the figure is shown (flat when they read the same), and the line from the hour before
    /// through the hours after it (where it was and where it goes, <see cref="LinePoints"/> of them). No hour, or none
    /// before the one under way: no direction.
    /// </summary>
    public static (TrendDirection? Direction, IReadOnlyList<double?> Series) Hours<THour>(
        IReadOnlyList<THour> hours, Func<THour, DateTimeOffset> at, Func<THour, double?> value, int digits, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(hours);
        ArgumentNullException.ThrowIfNull(at);
        ArgumentNullException.ThrowIfNull(value);
        if (hours.Count == 0)
        {
            return (null, []);
        }

        var current = 0;
        for (var i = 0; i < hours.Count; i++)
        {
            if (at(hours[i]) <= now)
            {
                current = i;
            }
        }

        MetricValue? Rounded(THour hour) => value(hour) is { } v && double.IsFinite(v)
            ? new MetricValue(Math.Round(v, digits).ToString(System.Globalization.CultureInfo.InvariantCulture), Math.Round(v, digits))
            : null;

        var direction = current > 0 && Rounded(hours[current]) is { } latest ? Direction(Rounded(hours[current - 1]), latest) : null;
        var series = hours.Skip(Math.Max(0, current - 1)).Take(LinePoints).Select(value).ToList();
        return (direction, series);
    }

    /// <summary>
    /// [S-14604] The fact <paramref name="key"/> of a system sheet as a metric: its value, number and state; a fact this
    /// read has not (Windows does not report it here, or not yet) is "--", unavailable, the metric's row kept.
    /// </summary>
    public static MetricShown Pick(DetailSheet sheet, string key)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        foreach (var fact in sheet.Facts)
        {
            if (string.Equals(fact.Key, key, StringComparison.Ordinal))
            {
                var available = fact.Available && !sheet.Absent;
                return new MetricShown(new MetricValue(fact.Value, available ? fact.Number : null, available)) { Name = fact.Label, Why = available ? null : sheet.Note };
            }
        }

        return new MetricShown(MetricValue.Unread) { Why = sheet.Note };
    }

    /// <summary>[S-14603] The glyph an arrow draws for <paramref name="direction"/> (checked against the font, [S-733]).</summary>
    public static string ArrowGlyph(TrendDirection direction) => direction switch
    {
        TrendDirection.Up => Glyphs.TrendUp,
        TrendDirection.Down => Glyphs.TrendDown,
        _ => Glyphs.TrendFlat,
    };
}

/// <summary>
/// [S-14603] The readings a single-metric widget saw, for an arrow where its source keeps no history: the newest and
/// the one before. A reading drawn again (the same stamp) replaces the newest; a new stamp moves it back.
/// </summary>
public sealed class MetricReadings
{
    private (long Stamp, MetricValue Value)? _latest;

    /// <summary>The reading before the newest; null until two were seen.</summary>
    public MetricValue? Previous { get; private set; }

    /// <summary>Take <paramref name="value"/> as reading <paramref name="stamp"/>.</summary>
    public void Add(long stamp, MetricValue value)
    {
        if (_latest is { } latest && latest.Stamp != stamp)
        {
            Previous = latest.Value;
        }

        _latest = (stamp, value);
    }

    /// <summary>Forget them (another place, drive or host: another metric's readings).</summary>
    public void Clear()
    {
        _latest = null;
        Previous = null;
    }
}
