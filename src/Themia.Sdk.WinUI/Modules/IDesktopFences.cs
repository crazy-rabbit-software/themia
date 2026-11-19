using Themia.Core.Desktop;
using Themia.Core.Files;

namespace Themia.Sdk.WinUI;

/// <summary>[S-13011] Where a fence draws one of its items now, in the fence widget's own DIPs (its top-left the card's).</summary>
public readonly record struct FenceItemBox(string Key, double X, double Y, double Width, double Height);

/// <summary>
/// [S-13000]..[S-13014] What a fence widget asks of the host's desktop (<see cref="IDesktopItems.Fences"/>): the
/// fence's items are the desktop's own, in the host's one selection ([S-5540]), so every gesture on one is the
/// desktop's (<see cref="IDesktopItems"/>, keyed by the item); what is the fence's alone (which items it holds, where
/// they are drawn, rolling up, its sort, its removal, drops into it, a paste or a new folder into it) is asked here,
/// by the fence's widget id, and carried out as one change of the state ([S-13010]: membership is undone with the
/// arrangement). The rules are Core's (<c>DesktopFences</c>); a fence never edits its own items. UI thread.
/// </summary>
public interface IDesktopFences
{
    /// <summary>[S-13011] The fence <paramref name="fenceId"/> draws these items where they say (only those it shows now): the band, the arrows and a drag find them there.</summary>
    void Laid(string fenceId, IReadOnlyList<FenceItemBox> boxes);

    /// <summary>[S-13012] Roll the fence up to its title bar, or unroll it to the height it had. Not an undo step: a look, as Fences' own.</summary>
    void RollUp(string fenceId, bool rolledUp);

    /// <summary>
    /// [S-13013] <paramref name="keys"/> in the order of <paramref name="by"/>, the desktop's own Sort by rule (shell items,
    /// folders, then files; the name breaking a tie): what a fence with a standing sort shows. Reads each file's size and
    /// date, so a fence asks when its items or its sort change, never per frame.
    /// </summary>
    IReadOnlyList<string> Order(IReadOnlyList<string> keys, DesktopSortBy by);

    /// <summary>[S-10910] The fence's title becomes <paramref name="name"/> (trimmed; empty is the kind's own word).</summary>
    void Rename(string fenceId, string name);

    /// <summary>
    /// [S-13009] Take the fence away, never a file: with <paramref name="putBack"/> its items arrive on the free desktop of
    /// its view at free spots, else they stay off that view (Add widget lists them). One undo step that brings the fence
    /// back with its items.
    /// </summary>
    void Remove(string fenceId, bool putBack);

    /// <summary>[S-13005] Whether a drag of the desktop's own icons runs now (dropped on a fence, its desktop items move in).</summary>
    bool DeskDragRunning { get; }

    /// <summary>
    /// [S-13005] The running drag of the desktop's icons is let go over the fence, before item <paramref name="index"/>: its
    /// desktop items leave the free desktop and any other fence of that view and come here, in their order; an own icon or a
    /// card it carries stays where it was. Returns the effect to answer (none when nothing came in).
    /// </summary>
    DropEffect DropDeskDrag(string fenceId, int index);

    /// <summary>
    /// [S-13005] Files from outside dropped on the fence: into the user's desktop folder through the shell's own drop
    /// target (as a drop on empty desktop: copy, move, link, the right-drag menu, virtual files), and what arrives under
    /// their names lands in the fence, not on the free desktop. [S-13102] What the drop did is one step of the undo log.
    /// Returns what it did.
    /// </summary>
    DropEffect DropFiles(string fenceId, Themia.App.Widgets.IWidgetDrag drag);

    /// <summary>[S-13005] Ctrl+V or the fence's Paste: the clipboard's files into the desktop folder, landing in the fence.</summary>
    void Paste(string fenceId);

    /// <summary>[S-13005] A new folder on the desktop, landing in the fence with its rename box open, as New › Folder.</summary>
    void NewFolder(string fenceId);

    /// <summary>The user's desktop folder (wherever it is redirected), where a drop on a fence goes; null when Windows names none.</summary>
    string? DesktopFolder { get; }

    /// <summary>
    /// [S-13019] The box one item stands in, in DIPs: a desktop icon's on the widget grid of the display the fence is on
    /// (the desktop's cell at the icon size in force rounded up to whole grid cells), so a fence's items keep the free
    /// icons' spacing. The desktop's plain cell where the fence is on no display.
    /// </summary>
    Themia.Sdk.SizeDip ItemBox(string fenceId);

    /// <summary>
    /// [S-13020] A left press on empty body of the fence: the desktop's own band starts there, on the surface it is drawn
    /// on, by the desktop's rules (no Ctrl or Shift: the selection goes first; it reaches the items of this fence, of other
    /// fences and the free icons alike, never the fence itself). Returns whether it started (the press is then the band's).
    /// </summary>
    bool Band(string fenceId, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs press);

    /// <summary>
    /// [S-13023] The files <paramref name="rules"/> and <paramref name="query"/> find (Windows Search over the query's folder,
    /// or the user's own folder, for <c>FenceSearch.Aqs</c>), checked against the rules, the newest first, at most the query's
    /// count (<c>FenceSearch.Shown</c>). Background work; never throws: a failed search is no file (logged).
    /// </summary>
    Task<IReadOnlyList<FoundFile>> SearchAsync(FenceRuleSet rules, FenceQuery query, CancellationToken token);

    /// <summary>[S-13021] The desktop items the running drag of the desktop's icons carries, in its order (none while none runs): a fence's ghost preview draws them.</summary>
    IReadOnlyList<DesktopItem> DraggedItems { get; }
}
