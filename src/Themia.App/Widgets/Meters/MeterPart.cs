using Themia.App.Services;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets.Meters;

/// <summary>What a meter part hands its readings to: the shared <see cref="MeterView"/>.</summary>
internal interface IMeterHost
{
    /// <summary>Put a reading on screen. May be called later than the tick that asked (a probe).</summary>
    void Present(MeterReading reading);

    /// <summary>Forget the history: the samples on the chart belong to something else now.</summary>
    void ClearHistory();

    /// <summary>[S-1167] Offer the card a title the kind knows (a drive's name, a host's label); null for the catalogue's.</summary>
    void SetTitle(string? title);

    /// <summary>[S-1167] Take a reading now rather than at the next tick (a Refresh command).</summary>
    void SampleNow();
}

/// <summary>
/// [S-1762] One meter kind's half of a meter: where the number comes from, what its chart plots and
/// how it is coloured. <see cref="MeterView"/> owns the other half — the reading, the history and
/// where each goes at each size ([S-1762]) — so a kind never lays anything out itself, and each kind
/// lives in its own file (<c>Meters/&lt;Kind&gt;Meter.cs</c>).
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

    /// <summary>[S-1123] The colour a reading deserves. Never applied to the reading's own text.</summary>
    public virtual MeterPressure PressureOf(MeterReading reading) => MeterPressure.Normal;

    /// <summary>
    /// [S-1187] A glyph drawn before the reading, in the pressure colour (the battery's fill level),
    /// or null for none. Count its width in <see cref="ReadingCharacters"/>.
    /// </summary>
    public virtual string? Badge(MeterReading reading) => null;

    /// <summary>
    /// Take a new config (it is already the right kind). True when the part needs a sample now —
    /// a ping host changed and waiting out the interval would read as a broken widget.
    /// </summary>
    public virtual bool ApplyConfig(WidgetConfig config, IMeterHost host) => false;

    /// <summary>
    /// Take one reading and hand it to <paramref name="host"/> — now, or later for a reading that
    /// needs a round trip. Runs on the UI thread's tick; a throw is caught and shown as unavailable.
    /// </summary>
    public abstract void Sample(IMeterHost host);

    /// <summary>[S-1167] Commands for the card's right-click menu (Refresh, Open in Explorer); read each time it opens.</summary>
    public virtual IReadOnlyList<WidgetAction> ContextActions(IMeterHost host) => [];

    /// <summary>[S-1167] An element for the right of the card's header (a status dot), or null. Read once.</summary>
    public virtual Microsoft.UI.Xaml.FrameworkElement? HeaderTrailing => null;

    /// <summary>Let go of anything held between samples (a probe, a counter lease).</summary>
    public virtual void Detach()
    {
    }

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
