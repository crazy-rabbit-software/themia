using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;

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

    public abstract WidgetKind Kind { get; }

    /// <summary>
    /// The widest reading this kind can produce, in characters ("100%" is 4, German "100 %" 5). The
    /// kind's worst case, never the live text, so the number does not change size between two samples.
    /// </summary>
    public virtual double ReadingCharacters => Formatting.Percent(1).Length;

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
    /// [S-1161] A short word on the reading's baseline after it, in the secondary colour behind the
    /// kind's glyph (the disk meter's drive), or null. A kind with one counts both in
    /// <see cref="ReadingCharacters"/>.
    /// </summary>
    public virtual string? Token => null;

    /// <summary>
    /// [S-2504] [S-2513] The hub series this config shows: the one the state asks the hub for, so
    /// the history is there from app start. A different one is a different history.
    /// </summary>
    public SeriesInterest Series(WidgetConfig config) => SystemInterests.For(config, DiskMetrics.SystemRoot).Series[0];

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

    /// <summary>The part for a meter kind.</summary>
    public static MeterPart For(WidgetKind kind) => kind switch
    {
        WidgetKind.Cpu => new CpuMeter(),
        WidgetKind.Memory => new MemoryMeter(),
        WidgetKind.Disk => new DiskMeter(),
        WidgetKind.Battery => new BatteryMeter(),
        WidgetKind.Network => new NetworkMeter(),
        WidgetKind.Ping => new PingMeter(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "not a meter"),
    };
}
