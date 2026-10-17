using Themia.App.Services.SystemData;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets.Details;

/// <summary>What a detail part hands its sheets to: the shared <see cref="DetailView"/>.</summary>
internal interface IDetailHost
{
    /// <summary>Put one sheet on screen. Called from the part's hub callbacks, on the UI thread.</summary>
    void Present(DetailSheet sheet);

    /// <summary>Offer the card a title the kind knows (an adapter's name, a host's label); null for the catalogue's.</summary>
    void SetTitle(string? title);

    /// <summary>[S-2510] Read now rather than at the next tick (Refresh).</summary>
    void SampleNow();
}

/// <summary>
/// [S-2400] One detail kind's half of a detail widget: which hub feeds its figures come from and what
/// they mean. <see cref="DetailView"/> owns the other half — where the hero, the facts and the list go
/// at each size ([S-2402]) — so a kind never lays anything out, and each kind lives in its own file
/// (<c>Details/&lt;Kind&gt;Detail.cs</c>).
/// <para>
/// [S-2500] A part reads nothing itself: it watches the system hub's feeds in <see cref="Watch"/> and
/// builds its sheet from their snapshots, and lets go of them in <see cref="Detach"/> ([S-1881]).
/// </para>
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

    /// <summary>
    /// Take a config (already the right kind) and hold the feeds it needs: on Attach and on every
    /// config change. A feed that is held already is kept; the sheet is presented from what the feeds
    /// hold, at once when they have been read.
    /// </summary>
    public abstract void Watch(SystemHub hub, WidgetConfig config, IDetailHost host);

    /// <summary>[S-2510] Read now (a Refresh command).</summary>
    public virtual void Refresh()
    {
    }

    /// <summary>Commands for the card's right-click menu, read each time it opens.</summary>
    public virtual IReadOnlyList<WidgetAction> ContextActions(IDetailHost host) => [];

    /// <summary>[S-2404] Enter on a row.</summary>
    public virtual void Open(DetailRow row)
    {
    }

    /// <summary>Let go of every feed. Safe twice.</summary>
    public abstract void Detach();

}
