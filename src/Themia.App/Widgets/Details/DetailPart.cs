using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets.Details;

/// <summary>What a detail part hands its reads to: the shared <see cref="DetailView"/>.</summary>
internal interface IDetailHost
{
    /// <summary>Put one read on screen. May come later than the tick that asked (a probe, a pool read).</summary>
    void Present(DetailSheet sheet);

    /// <summary>Offer the card a title the kind knows (an adapter's name, a host's label); null for the catalogue's.</summary>
    void SetTitle(string? title);

    /// <summary>Read now rather than at the next tick (Refresh).</summary>
    void SampleNow();
}

/// <summary>
/// [S-2400] One detail kind's half of a detail widget: where its figures come from and what they
/// mean. <see cref="DetailView"/> owns the other half — where the hero, the facts and the list go at
/// each size ([S-2402]) — so a kind never lays anything out, and each kind lives in its own file
/// (<c>Details/&lt;Kind&gt;Detail.cs</c>).
/// </summary>
internal abstract class DetailPart
{
    protected const string Scope = "details";

    public abstract WidgetKind Kind { get; }

    /// <summary>Whether the kind has a main figure at all (a list may not).</summary>
    public virtual bool HasHero => true;

    /// <summary>The widest the hero can be, in characters: the kind's worst case, never the live text.</summary>
    public virtual double HeroCharacters => 8;

    /// <summary>One list row's height; rows with a second line or a bar are taller.</summary>
    public virtual double ListRowHeight => Fluent.RowHeightCompact;

    /// <summary>[S-2412] At most this many rows (0: as many as there are).</summary>
    public virtual int MaxRows => 0;

    /// <summary>[S-2420] Whether a few rows grow into a tall tile (drives) rather than leave it empty.</summary>
    public virtual bool RowsGrow => false;

    /// <summary>[S-2404] Whether Enter on a row does something (opens a drive).</summary>
    public virtual bool RowsOpen => false;

    /// <summary>Take a new config (already the right kind). True when a read is due now (the host changed).</summary>
    public virtual bool ApplyConfig(WidgetConfig config, IDetailHost host) => false;

    /// <summary>[S-1881] Take the shared readers' leases; only from the view's Attach.</summary>
    public virtual void Attach()
    {
    }

    /// <summary>Read once and hand the sheet to <paramref name="host"/>. Runs on the UI thread's tick.</summary>
    public abstract void Sample(IDetailHost host);

    /// <summary>Commands for the card's right-click menu, read each time it opens.</summary>
    public virtual IReadOnlyList<WidgetAction> ContextActions(IDetailHost host) => [];

    /// <summary>[S-2404] Enter on a row.</summary>
    public virtual void Open(DetailRow row)
    {
    }

    /// <summary>Let go of every lease and anything in flight. Safe twice.</summary>
    public virtual void Detach()
    {
    }

    /// <summary>The part for a detail kind.</summary>
    public static DetailPart For(WidgetKind kind) => kind switch
    {
        WidgetKind.CpuDetails => new CpuDetailPart(),
        WidgetKind.MemoryDetails => new MemoryDetailPart(),
        WidgetKind.TopProcesses => new ProcessesPart(),
        WidgetKind.NetworkDetails => new NetworkDetailPart(),
        WidgetKind.DiskSpace => new DiskSpacePart(),
        WidgetKind.BatteryDetails => new BatteryDetailPart(),
        WidgetKind.PingDetails => new PingDetailPart(),
        WidgetKind.GpuDetails => new GpuDetailPart(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "not a detail widget"),
    };
}
