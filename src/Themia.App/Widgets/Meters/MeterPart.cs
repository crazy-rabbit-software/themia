using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Widgets.Meters;

/// <summary>What a meter part may ask of the shared <see cref="MeterView"/>.</summary>
internal interface IMeterHost
{
    /// <summary>[S-1167] Offer the card a title the kind knows (a drive's name, a host's label); null for the catalogue's.</summary>
    void SetTitle(string? title);

    /// <summary>[S-1167] [S-2510] Read now rather than at the next tick (a Refresh command).</summary>
    void SampleNow();
}

/// <summary>
/// [S-6300] What a meter's style draws besides its reading: the history ([S-1762]), a gauge round
/// the reading, or the kind's items (cores, a used/free bar) as bars or tiles. The shared
/// <see cref="MeterView"/> lays each on the same ladder; a kind only says which (<see cref="MeterPart.Look"/>).
/// </summary>
internal enum MeterLook
{
    /// <summary>[S-1762] The reading over its history.</summary>
    Graph = 0,

    /// <summary>[S-6302] The reading inside a half-circle gauge of the same share.</summary>
    Gauge = 1,

    /// <summary>[S-6303] The reading over a vertical bar per item (<see cref="MeterPart.WatchItems"/>).</summary>
    Columns = 2,

    /// <summary>[S-6304] The reading over a labelled bar row per item.</summary>
    Rows = 3,

    /// <summary>[S-6305] The reading over a tile per item, shaded by its load.</summary>
    Tiles = 4,

    /// <summary>[S-6264] The reading and its second figure (upload beside download), both large, no history.</summary>
    Numbers = 5,

    /// <summary>[S-6262] A battery filled to the reading, the figure beside or under it.</summary>
    Battery = 6,

    /// <summary>[S-6266] A ring filled to the reading, the figure inside it.</summary>
    Ring = 7,

    /// <summary>[S-6263] Signal bars by the reading's latency, the figure beside or under them.</summary>
    Signal = 8,

    /// <summary>[S-6317] The reading over a tile per item, each the item's own history and figure.</summary>
    Graphs = 9,
}

/// <summary>[S-6300] One item a bar or tile look draws: its share (null: no reading), its colour, and a row's words.</summary>
internal readonly record struct MeterItem(double? Fraction, MeterPressure Pressure, string? Label = null, string? Value = null);

/// <summary>[S-6300] A kind's items as the hub reads them: the latest, and every read after through the callback.</summary>
internal sealed class MeterItemsWatch : IDisposable
{
    private readonly IDisposable _watch;
    private readonly Func<IReadOnlyList<MeterItem>?> _current;
    private readonly Func<int, int, SeriesPoint[]>? _history;
    private readonly Func<long>? _added;

    public MeterItemsWatch(IDisposable watch, Func<IReadOnlyList<MeterItem>?> current, Func<int, int, SeriesPoint[]>? history = null, Func<long>? added = null)
    {
        _watch = watch;
        _current = current;
        _history = history;
        _added = added;
    }

    /// <summary>[S-2506] The items read so far, or null before the first read.</summary>
    public IReadOnlyList<MeterItem>? Current => _current();

    /// <summary>[S-6318] An item's newest <paramref name="max"/> points as the hub keeps them, oldest first; empty for a kind that keeps none.</summary>
    public SeriesPoint[] History(int item, int max) => _history?.Invoke(item, max) ?? [];

    /// <summary>[S-1121] How many points the items' history ever had: the newest one's absolute position.</summary>
    public long Added => _added?.Invoke() ?? 0;

    public void Dispose() => _watch.Dispose();

    /// <summary>Items mapped from one hub source, and the items' history when the source keeps one.</summary>
    public static MeterItemsWatch Of<T>(
        ISystemFeeds hub,
        SourceId<T> source,
        Func<T, IReadOnlyList<MeterItem>> map,
        Action<IReadOnlyList<MeterItem>> onItems,
        Func<T, int, int, SeriesPoint[]>? history = null,
        Func<T, long>? added = null)
    {
        var watch = hub.Watch(source, snapshot => onItems(map(snapshot)));
        return new MeterItemsWatch(
            watch,
            () => watch.HasValue ? map(watch.Value) : null,
            history is null ? null : (item, max) => watch.HasValue ? history(watch.Value, item, max) : [],
            added is null ? null : () => watch.HasValue ? added(watch.Value) : 0);
    }
}

/// <summary>
/// [S-1762] One meter kind's half of a meter: which series it shows, what its chart plots and how it
/// is coloured. <see cref="MeterView"/> owns the other half — the reading, the history and where each
/// goes at each size ([S-1762]) — so a kind never lays anything out itself, and each kind lives in
/// its own file (<c>Meters/&lt;Kind&gt;Meter.cs</c>).
/// <para>
/// [S-2500] A part reads nothing: the readings and their history come from the system hub's series
/// (<see cref="Series"/>), which every meter of the same series shares.
/// </para>
/// </summary>
internal abstract class MeterPart
{
    protected const string Scope = "meter";

    public abstract WidgetId Kind { get; }

    /// <summary>
    /// [S-3860] The widest reading this kind can produce ("100%", German "100 %"), its number and
    /// unit counted apart at the sizes they are drawn. The kind's worst case, never the live
    /// text, so the number does not change size between two samples.
    /// </summary>
    public virtual HeroBound Reading => HeroBound.Of(Formatting.Percent(1));

    /// <summary>[S-1124] What the top of the chart stands for.</summary>
    public virtual ChartScale Scale => ChartScale.Share;

    /// <summary>Whether the measured thing can be missing from the machine (a battery), which shows an empty state.</summary>
    public virtual bool CanBeAbsent => false;

    /// <summary>What the meter shows before its series has a reading (a probe still out).</summary>
    public virtual MeterReading Waiting => new(null, "--", false);

    /// <summary>[S-1123] The colour a reading deserves. Never applied to the reading's own text.</summary>
    public virtual MeterPressure PressureOf(MeterReading reading) => MeterPressure.Normal;

    /// <summary>
    /// [S-1187] A glyph drawn before the reading, in the pressure colour (the battery's fill level),
    /// or null for none. A kind with one says so in <see cref="HasBadge"/>.
    /// </summary>
    public virtual string? Badge(MeterReading reading) => null;

    /// <summary>[S-3860] Whether the reading may lead with a <see cref="Badge"/>, which the ladder measures as the readout's glyph.</summary>
    public virtual bool HasBadge => false;

    /// <summary>
    /// [S-1161] The reading's tooltip while it is available: what the reading is about (the disk
    /// meter's drive), so a card whose header is switched off still says it; null for none.
    /// </summary>
    public virtual string? ReadingTip => null;

    /// <summary>
    /// [S-2504] [S-2513] The hub series this config shows: the one the state asks the hub for, so
    /// the history is there from app start. A different one is a different history.
    /// </summary>
    public SeriesInterest Series(WidgetConfig config) => SystemInterests.For(config, SystemDrive.Root).Series[0];

    /// <summary>Take a new config (it is already the right kind): a title, a drive.</summary>
    public virtual void ApplyConfig(WidgetConfig config, IMeterHost host)
    {
    }

    /// <summary>One reading reached the screen: for what a kind derives from it (a title, a status dot).</summary>
    public virtual void Observe(MeterReading reading, IMeterHost host)
    {
    }

    /// <summary>[S-1167] Commands for the card's right-click menu (Refresh, Open in Explorer); read each time it opens.</summary>
    public virtual IReadOnlyList<WidgetAction> ContextActions(IMeterHost host) => [];

    /// <summary>[S-1167] An element for the right of the card's header (a status dot), or null. Read once.</summary>
    public virtual Microsoft.UI.Xaml.FrameworkElement? HeaderTrailing => null;

    /// <summary>[S-6300] What this config's style draws besides the reading; the history for a kind without styles.</summary>
    public virtual MeterLook Look(WidgetConfig config) => MeterLook.Graph;

    /// <summary>
    /// [S-6300] Watch the items a <see cref="MeterLook.Columns"/>, <see cref="MeterLook.Rows"/> or
    /// <see cref="MeterLook.Tiles"/> look draws; null for a kind that has none. Never calls back
    /// from inside this call ([S-2506]): what is already read is <see cref="MeterItemsWatch.Current"/>.
    /// </summary>
    public virtual MeterItemsWatch? WatchItems(ISystemFeeds hub, Action<IReadOnlyList<MeterItem>> onItems) => null;

    /// <summary>[S-6304] The widest row line a Rows look writes (label and value, in characters), for the rows' type step.</summary>
    public virtual double ItemLineCharacters => 0;

    /// <summary>
    /// [S-6325] The reading as the number its readout glides through, or null for one that is not a
    /// number (unavailable, waiting): a share for a percentage meter, the value for the others.
    /// </summary>
    public virtual double? Number(MeterReading reading) => reading.Available && reading.Value is null ? reading.Fraction : null;

    /// <summary>[S-6325] A number on the way to a reading, written as the reading is.</summary>
    public virtual string Format(double number) => Formatting.Percent(number);

    /// <summary>[S-6325] The number's own step: a whole percent of a share.</summary>
    public virtual double Resolution => 0.01;

    /// <summary>[S-1917] Debug renders: fixed items for the bar and tile looks (<c>--state sample</c>).</summary>
    public virtual IReadOnlyList<MeterItem> SampleItems => [];

    /// <summary>[S-1917] Debug renders: the fixed reading <c>--state sample</c> shows, in this kind's own terms.</summary>
    public virtual MeterReading SampleReading => new(0.62, Formatting.Percent(0.62), true);

}
