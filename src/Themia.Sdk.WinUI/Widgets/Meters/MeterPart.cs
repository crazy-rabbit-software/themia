using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.Core.Links;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.App.Widgets.Meters;

/// <summary>What a meter part may ask of the shared <see cref="MeterView"/>.</summary>
public interface IMeterHost
{
    /// <summary>[S-1167] Offer the card a title the kind knows (a drive's name: the user's own source); null for the catalogue's.</summary>
    void SetTitle(string? title);

    /// <summary>[S-2260] Which one the meter reads (an adapter, a host) as the header's label beside the widget's name; null for none.</summary>
    void SetAbout(string? about);

    /// <summary>[S-1167] [S-2510] Read now rather than at the next tick (a Refresh command).</summary>
    void SampleNow();

    /// <summary>
    /// [S-433] [S-1940] The thing measured is not there (no adapter at the chosen place), with why, or is
    /// back (null): the meter is drawn absent, the reason on hover, and its series ignored meanwhile.
    /// </summary>
    void SetAbsent(string? reason);
}

/// <summary>[S-6300] One item a bar or tile look draws: its share (null: no reading), its colour, and a row's words.</summary>
public readonly record struct MeterItem(double? Fraction, MeterPressure Pressure, string? Label = null, string? Value = null);

/// <summary>[S-6300] A kind's items as the hub reads them: the latest, and every read after through the callback.</summary>
public sealed class MeterItemsWatch : IDisposable
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
public abstract class MeterPart
{
    protected const string Scope = "meter";

    public abstract WidgetId Kind { get; }

    /// <summary>
    /// [S-6702] The widest reading this kind can produce ("100%", German "100 %"), split as the Readout
    /// draws it. The kind's worst case, never the live text, so the number does not change step between
    /// two samples.
    /// </summary>
    public virtual ReadoutText Reading => ReadoutRules.Split(Formatting.Percent(1));

    /// <summary>[S-1124] What the top of the chart stands for.</summary>
    public virtual ChartScale Scale => ChartScale.Share;

#if DEBUG
    /// <summary>
    /// [S-1940] Debug renders: the reading <c>--state unavailable</c> shows where the measured thing can be
    /// missing from the machine (a battery), or null for a kind that is always there.
    /// </summary>
    public virtual MeterReading? AbsentReading => null;
#endif

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

    /// <summary>[S-2001] What a click on the card opens for this config (Task Manager, a Settings page); null for nothing.</summary>
    public virtual CardOpen? Opens(WidgetConfig config) => null;

    /// <summary>
    /// [S-2504] [S-2513] The hub series this config shows: the one the state asks the hub for, so
    /// the history is there from app start. A different one is a different history.
    /// </summary>
    public SeriesInterest Series(WidgetConfig config) => SystemInterests.For(config, SystemDrive.Root).Series[0];

    /// <summary>Take a new config (it is already the right kind): a title, a drive.</summary>
    public virtual void ApplyConfig(WidgetConfig config, IMeterHost host)
    {
    }

    /// <summary>
    /// [S-2504] Watch what the kind needs besides its series (the GPU's adapters, for its name and
    /// whether it is there), with the meter's series; never calls back from inside this call. Let go in
    /// <see cref="Unwatch"/>.
    /// </summary>
    public virtual void Watch(ISystemFeeds hub, IMeterHost host)
    {
    }

    /// <summary>Let go of what <see cref="Watch"/> held. Safe twice.</summary>
    public virtual void Unwatch()
    {
    }

    /// <summary>One reading reached the screen: for what a kind derives from it (a title, a problem glyph).</summary>
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
    /// [S-11000] Which of its subject's atoms this face is: the reading (the default), the history alone,
    /// or the items alone (the cores, the engines).
    /// </summary>
    public virtual MeterAtom Atom => MeterAtom.Reading;

    /// <summary>
    /// [S-6300] Watch the items a <see cref="MeterLook.Columns"/>, <see cref="MeterLook.Rows"/> or
    /// <see cref="MeterLook.Tiles"/> look draws; null for a kind that has none. Never calls back
    /// from inside this call ([S-2506]): what is already read is <see cref="MeterItemsWatch.Current"/>.
    /// </summary>
    public virtual MeterItemsWatch? WatchItems(ISystemFeeds hub, Action<IReadOnlyList<MeterItem>> onItems) => null;

    /// <summary>
    /// [S-11910] [S-11906] The view-links topic of a moment pointed at on a history chart, named by the kind
    /// that takes part (<c>LinkTopics.HoveredInstant</c>), or null for none: a history atom (and the cores'
    /// graphs) publishes the moment under the pointer and marks the moment pointed at elsewhere; a reading
    /// atom shows its value at a moment pointed at on a chart of its own subject ([S-11912]).
    /// </summary>
    public virtual LinkTopic<InstantLink>? Instant => null;

    /// <summary>
    /// [S-11953] [S-11906] The view-links topic of a drive pointed at, named by the kind that is about one drive
    /// (<c>LinkTopics.HoveredDrive</c>), or null for none: the card publishes its drive (<see cref="DriveOf"/>) while
    /// the pointer is on it and takes the item layer's hover fill while it is pointed at elsewhere ([S-11903]).
    /// </summary>
    public virtual LinkTopic<DriveLink>? Drive => null;

    /// <summary>[S-11952] The drive this config is about, or null (every drive together, a kind about no drive).</summary>
    public virtual DriveLink? DriveOf(WidgetConfig config) => null;

    /// <summary>
    /// [S-11912] One point of the series as the reading writes it ("32%", "1.2 MB/s"), for a reading standing
    /// at a moment of its history; null when the point says nothing (the kind's own reading then stays).
    /// </summary>
    public virtual string? ValueAt(SeriesPoint point)
        => Scale.FollowsWindow || !double.IsFinite(point.First) ? null : Formatting.Percent(Math.Clamp(point.First, 0, 1));

    /// <summary>[S-1940] The items a look draws for a thing not on this PC (the GPU's engines, each "--"); none by default.</summary>
    public virtual IReadOnlyList<MeterItem> AbsentItems => [];
}
