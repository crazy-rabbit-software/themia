using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-14603] What a single-metric widget draws with its value (owner, 2026-11-20: "you should also have the option to
/// show trendlines as well or just the trend with arrows"): nothing, an arrow beside the value (up, down or flat against
/// the reading before), or a trend line (the metric's own history as a graph on the one chart under the reading, [S-14220]).
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

    /// <summary>
    /// [S-14703] How a value of <see cref="Series"/> is written as the shown figure is (a Detailed graph's values, its low,
    /// its high and its latest: "1,013 hPa"); null where the family writes none (plain numbers).
    /// </summary>
    public Func<double, string>? Figure { get; init; }
}

/// <summary>[S-14701] How a single metric's reading stands in its box: the facts' row, or the readout block.</summary>
public enum MetricFace
{
    /// <summary>One row tall: the fact row (glyph, name, value, the arrow beside it).</summary>
    Row,

    /// <summary>Taller: the value as the reading and the name as its caption, one block scaled up together.</summary>
    Readout,
}

/// <summary>[S-14701] How a readout's caption (the glyph and the name) stands to its value.</summary>
public enum MetricArrangement
{
    /// <summary>On one line, the caption before the value: a wide, short box.</summary>
    Beside,

    /// <summary>The caption over the value: a narrow or tall box.</summary>
    Stacked,
}

/// <summary>
/// [S-14700] [S-14702] What a single metric's box holds (<see cref="MetricRules.Look"/>): the reading's face and its height
/// from the card's top edge, and the graph's height under it at the card's foot (0: no graph). DIPs of the card's box.
/// </summary>
public readonly record struct MetricLook(MetricFace Face, double Reading, double Graph);

/// <summary>
/// [S-14706] What a click on a single metric opens (owner, 2026-11-20: "they should open the datasource when clicked /
/// page that shows that metric"), declared on its kind (<see cref="MetricKind.Opens"/>): the target for a card's config
/// (null: nothing now, a place not set) and the card menu's line that opens it ("Open Task Manager", "Open on MSN Weather").
/// </summary>
public sealed record MetricOpen(LocalText Label, Func<WidgetConfig, CardOpen?> Target);

/// <summary>
/// [S-14706] The targets the single metrics' families open, each the one their family's own widgets open ([S-2006],
/// [S-2011], [S-2019], [S-2023], [S-2032]): Task Manager (the processor, the memory, the graphics), Settings' Network &amp;
/// internet (the network, the ping), Settings' Power &amp; battery (the battery), MSN Weather's forecast for the card's
/// place (the weather, the air), the drive in File Explorer (a drive's facts).
/// </summary>
public static class MetricOpens
{
    /// <summary>[S-2011] Task Manager.</summary>
    public static MetricOpen TaskManager { get; } = new(Str.Metric_Open_TaskManager.Local(), _ => CardOpen.TaskManager);

    /// <summary>[S-2019] Settings' Network &amp; internet.</summary>
    public static MetricOpen Network { get; } = new(Str.Metric_Open_Network.Local(), _ => CardOpen.Settings("network-status"));

    /// <summary>[S-2023] Settings' Power &amp; battery.</summary>
    public static MetricOpen Battery { get; } = new(Str.Metric_Open_Battery.Local(), _ => CardOpen.Settings("batterysaver"));

    /// <summary>[S-2006] MSN Weather's forecast for the card's place, in the language's market and the unit shown; no place, nothing.</summary>
    public static MetricOpen Weather { get; } = new(Str.Metric_Open_Weather.Local(),
        config => config is WeatherConfig weather ? WeatherPages.Forecast(weather, RegionFormat.Current.Temperature, Strings.Current) : null);

    /// <summary>[S-2032] The card's drive in File Explorer (the system drive while none is chosen), as the Drive widget opens it.</summary>
    public static MetricOpen Drive { get; } = new(Str.Metric_Open_Drive.Local(),
        config => config is IDriveChoice drive ? CardOpen.Place(DiskRules.Root(drive.Drive) ?? DiskRules.Root(SystemDrive.Root)) : null);
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
/// [S-14600]..[S-14605] [S-14700]..[S-14703] The single-metric widget's rules, pure, one base for every one-reading widget
/// (owner, 2026-11-20: "split all the other widgets that are alike to the weather and air facts into individual metrics too.
/// use the same base component for all of them"): its size (one fact row at its least, a graph's least under it, every
/// cell taller too), how its face fits its box, the default without a reading, the trends a history allows, the arrow's
/// direction and the trend graph's model.
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
    /// [S-14600] [S-14500] The one row's optimal height: a 28 fact row in whole grid cells, three on the default grid, from
    /// the card's top edge to its foot edge, so the card's top and bottom spacing is a list row's (owner, 2026-11-20: "the
    /// individual weather facts like pressure have a way to high height. the contents should also fit to the height").
    /// </summary>
    public static double RowHeight => RowOn(ItemGrid.Default());

    /// <summary>[S-14600] The one row's optimal height on <paramref name="grid"/>: the 28 row in its whole cells.</summary>
    public static double RowOn(ItemGrid grid) => ItemStretch.Optimal(FamilyShape.Row1, grid);

    /// <summary>[S-14600] The sizing of a single metric drawing no trend: 200..420 wide (relaxed), one row tall.</summary>
    public static WidgetSizing Sizing { get; } = SizingFor(MetricTrend.None);

    /// <summary>
    /// [S-14702] [S-6751] The least height a metric's graph takes at the card's foot, on <paramref name="grid"/>: a whole row
    /// for its line to move in (as a meter's history under its figure, [S-14220]) and, <paramref name="detail"/> Detailed,
    /// the room the one chart keeps for what it writes over and under its line (<see cref="ChartPlot.Keeps"/>: the high's
    /// figure over it, the low's and the foot's line under it), in whole cells, so the line keeps its row whatever is written.
    /// </summary>
    public static double GraphLeast(ChartDetail detail, ItemGrid grid)
    {
        // What the chart keeps under an owner's part over its plot (one DIP stands for it) and at its foot.
        var (top, bottom) = ChartPlot.Keeps(new ChartModel { Detail = detail, Over = 1 });
        return grid.Whole(RowOn(grid) + Math.Max(0, top - 1) + Math.Max(0, bottom));
    }

    /// <summary>
    /// [S-14600] [S-14603] [S-14700] [S-14500] The sizing of a single metric drawing <paramref name="trend"/> in
    /// <paramref name="detail"/>: its least and natural height its one row, and a graph's least under it where it draws one
    /// (<see cref="GraphLeast"/>: a row, a Detailed graph's room more); every cell taller too (owner, 2026-11-20: "the widgets
    /// like the PM2.5 one should be resizable in height and change its style so that it fits"; the descriptor relaxes the
    /// height, [S-12411]), its face changing its look to fit the box (<see cref="Look"/>); 200..420 wide and, beside the
    /// value, the arrow's room with its gap, so the name and the value keep theirs (content decides the size, [S-2445]); a
    /// graph takes none beside it, it is under the reading.
    /// </summary>
    public static WidgetSizing SizingFor(MetricTrend trend, ChartDetail detail = ChartDetail.Simple)
    {
        var room = TrendRoom(trend);
        var grid = ItemGrid.Default();
        var height = RowOn(grid) + (trend == MetricTrend.Line ? GraphLeast(detail, grid) : 0);
        return WidgetSizing.Range(new(MinWidth + room, height), new(NaturalWidth + room, height), new(MaxWidth + room, height));
    }

    /// <summary>
    /// [S-14603] The width a trend takes beside the value: the arrow its own and the gap before the value; a line none, it
    /// is the graph under the reading ([S-14220]).
    /// </summary>
    public static double TrendRoom(MetricTrend trend) => trend switch
    {
        MetricTrend.Arrow => FamilyShape.Icon + FamilySpace.ColumnGap,
        _ => 0,
    };

    /// <summary>[S-14600] [S-14703] The sizing of <paramref name="config"/>: by the trend it draws (none for a config without one) and its graph's detail.</summary>
    public static WidgetSizing SizingOf(WidgetConfig config)
        => SizingFor(config is IMetricTrend trend ? Effective(trend) : MetricTrend.None, config is IChartLooks looks ? ChartLooks.EffectiveDetail(looks) : ChartDetail.Simple);

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
    /// [S-14700] [S-14701] [S-14702] How a single metric fills a card <paramref name="height"/> tall (DIPs of the card's box,
    /// its top edge to its foot edge) on <paramref name="grid"/> (owner, 2026-11-20: "the widgets like the PM2.5 one should be
    /// resizable in height and change its style so that it fits"): the same parts at every size (the name, the value, its
    /// arrow, its graph), arranged by the box, never chosen by it ([S-2445]).
    /// <list type="bullet">
    /// <item>No graph: one row tall (under two whole rows) the facts' row; two rows or more the readout, the whole card.</item>
    /// <item>A graph (<paramref name="trend"/> Line): it takes the foot, never less than <see cref="GraphLeast"/>; the reading
    /// takes the top, growing first, on whole cells, to about half the card (the half rounded up), the graph the rest. The
    /// reading is the row while the card is under three rows or the reading's share is one row, else the readout.</item>
    /// </list>
    /// A box not laid out yet (no height) is the row with no graph.
    /// </summary>
    public static MetricLook Look(double height, MetricTrend trend, ChartDetail detail, ItemGrid grid)
    {
        if (!(height > 0) || !double.IsFinite(height))
        {
            return new MetricLook(MetricFace.Row, 0, 0);
        }

        var pitch = grid.Pitch;
        var row = grid.Cells(RowOn(grid));
        // The card's whole cells on the view's pitch, a fiftieth's slack as an item's (the host lays it on its own axis pitch).
        var cells = (int)Math.Floor((height / pitch) + ItemGrid.Slack);
        var rows = cells / Math.Max(1, row);
        if (trend != MetricTrend.Line)
        {
            return new MetricLook(rows >= 2 ? MetricFace.Readout : MetricFace.Row, height, 0);
        }

        var graph = grid.Cells(GraphLeast(detail, grid));
        var half = (cells + 1) / 2;
        var reading = Math.Max(Math.Min(row, cells), Math.Min(half, cells - graph));
        var top = Math.Min(height, reading * pitch);
        var face = rows >= 3 && reading > row ? MetricFace.Readout : MetricFace.Row;
        return new MetricLook(face, top, Math.Max(0, height - top));
    }

    /// <summary>
    /// [S-14701] How a readout's caption stands to its value in a room <paramref name="width"/> × <paramref name="height"/>
    /// (the block's room: the card's less its insets): the arrangement whose block, its parts at their own size
    /// (<paramref name="caption"/>, <paramref name="value"/> and <paramref name="gap"/> between them beside each other), scales
    /// up larger in it; beside only where it is strictly larger, so a narrow or tall box stands the name over the value.
    /// </summary>
    public static MetricArrangement Arrange(double width, double height, SizeDip caption, SizeDip value, double gap)
    {
        static double Scale(double w, double h, double bw, double bh)
            => bw > 0 && bh > 0 ? Math.Min(w / bw, h / bh) : 0;

        if (!(width > 0) || !(height > 0) || !double.IsFinite(width) || !double.IsFinite(height))
        {
            return MetricArrangement.Stacked;
        }

        var beside = Scale(width, height, caption.Width + Math.Max(0, gap) + value.Width, Math.Max(caption.Height, value.Height));
        var stacked = Scale(width, height, Math.Max(caption.Width, value.Width), caption.Height + value.Height);
        return beside > stacked + 1e-9 ? MetricArrangement.Beside : MetricArrangement.Stacked;
    }

    /// <summary>
    /// [S-14220] [S-14703] The metric's graph on the one chart ([S-6750]) <paramref name="width"/> wide, in its chart look
    /// (<paramref name="type"/>, <paramref name="detail"/>): the newest <see cref="LinePoints"/> values of
    /// <paramref name="series"/>, oldest first, on their own range (the low at the band's foot, the high at its top; a flat
    /// history a level line through the middle; a column's foot a third of the range under the low, so the lowest is a
    /// short column, never none). A line's points stand on the band's slots, the newest on the right edge, a history still
    /// filling growing in from the right ([S-6331]); a column a slot each, the newest the rightmost. A gap is left out; with
    /// fewer than two values the chart lies at its baseline, never nothing (WR-10). The reading stands over the plot's top
    /// (<paramref name="over"/>, the line kept under it); the values a Detailed graph writes are the shown figure's
    /// (<paramref name="figure"/>).
    /// </summary>
    public static ChartModel Graph(IReadOnlyList<double?>? series, double width, ChartType type, ChartDetail detail, double over, Func<double, string>? figure = null)
    {
        var model = new ChartModel { Type = type, Detail = detail, Over = double.IsFinite(over) ? Math.Max(0, over) : 0, Format = figure };
        var values = (series ?? []).TakeLast(LinePoints).Where(v => v is { } x && double.IsFinite(x)).Select(v => v!.Value).ToList();
        if (values.Count < 2 || !(width > 0) || !double.IsFinite(width))
        {
            return model;
        }

        var low = values.Min();
        var high = values.Max();
        var span = high - low;
        double[] xs;
        if (type == ChartType.Bars)
        {
            var cell = width / LinePoints;
            xs = [.. values.Select((_, i) => width - ((values.Count - i - 0.5) * cell))];
        }
        else
        {
            xs = SparkSlots.Layout(values.Count, values.Count, 1, LinePoints, width, FamilyShape.Stroke).X;
        }

        var foot = type == ChartType.Bars && span > 1e-9 ? low - (span / 3) : low;
        return model with
        {
            Low = foot,
            High = span > 1e-9 ? high : low,
            Series = [.. values.Select((v, i) => new ChartSample(xs[i], v))],
        };
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
