using Themia.App.Services.SystemData;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets.Details;

/// <summary>What a detail part hands its sheets to: the shared <see cref="DetailView"/>.</summary>
public interface IDetailHost
{
    /// <summary>Put one sheet on screen. Called from the part's hub callbacks, on the UI thread.</summary>
    void Present(DetailSheet sheet);

    /// <summary>[S-2260] Which one the kind reads (a processor, an adapter, a host) as the header's label beside the widget's name; null for none.</summary>
    void SetAbout(string? about);

    /// <summary>[S-2510] Read now rather than at the next tick (Refresh).</summary>
    void SampleNow();
}

/// <summary>[S-11007] [S-11008] A detail face that is an atom: fact rows alone, or a list alone.</summary>
public enum DetailAtom
{
    Facts = 0,
    List = 1,
}

/// <summary>
/// [S-2400] One detail kind's half of a detail widget: which hub feeds its figures come from and what
/// they mean. <see cref="DetailView"/> owns the other half — where the hero, the facts and the list go
/// at each size ([S-2402], <see cref="DetailFace"/>) — so a kind never lays anything out, and each kind lives in its own file
/// (<c>Details/&lt;Kind&gt;Detail.cs</c>).
/// <para>
/// [S-2500] A part reads nothing itself: it watches the system hub's feeds in <see cref="Watch"/> and
/// builds its sheet from their snapshots, and lets go of them in <see cref="Detach"/> ([S-1881]).
/// </para>
/// </summary>
public abstract class DetailPart
{
    protected const string Scope = "details";

    public abstract WidgetId Kind { get; }

    /// <summary>
    /// [S-11007] [S-11008] Which atom the face is: its kind's facts as a pick (<see cref="FactOptions"/>,
    /// the config a <c>FactsConfig</c>), or its list of a set number of rows (the config an <c>IKitRows</c>).
    /// </summary>
    public abstract DetailAtom Atom { get; }

    /// <summary>[S-11007] Every fact a facts atom can pick, in its kind's order.</summary>
    public virtual IReadOnlyList<FactOption> FactOptions => [];

    /// <summary>[S-11008] How many lines a list atom's rows have (1 or 2): its pitch follows.</summary>
    public virtual int ListLines => 1;

    /// <summary>[S-2404] Whether Enter on a row does something (opens a drive).</summary>
    public virtual bool RowsOpen => false;

    /// <summary>
    /// Take a config (already the right kind) and hold the feeds it needs: on Attach and on every
    /// config change. A feed that is held already is kept; the sheet is presented from what the feeds
    /// hold, at once when they have been read.
    /// </summary>
    public abstract void Watch(ISystemFeeds hub, WidgetConfig config, IDetailHost host);

    /// <summary>[S-2510] Read now (a Refresh command).</summary>
    public virtual void Refresh()
    {
    }

    /// <summary>Commands for the card's right-click menu, read each time it opens.</summary>
    public virtual IReadOnlyList<WidgetAction> ContextActions(IDetailHost host) => [];

    /// <summary>[S-2001] What a click on the card, off its rows, opens for this config; null for nothing.</summary>
    public virtual CardOpen? Opens(WidgetConfig config) => null;

    /// <summary>[S-2404] Enter on a row.</summary>
    public virtual void Open(DetailRow row)
    {
    }

    /// <summary>
    /// [S-2437] A row's own right-click menu (and the Menu key's, Shift+F10), read each time it opens;
    /// empty, the default, leaves the right-click to the card's menu.
    /// </summary>
    public virtual IReadOnlyList<ItemMenuEntry> RowMenu(DetailRow row) => [];

    /// <summary>Let go of every feed. Safe twice.</summary>
    public abstract void Detach();

#if DEBUG
    /// <summary>[S-1942] Debug renders: the sheet <c>--state unavailable</c> shows, a thing not on this machine; null for a kind that cannot be absent.</summary>
    public virtual DetailSheet? AbsentSheet() => null;
#endif

}
