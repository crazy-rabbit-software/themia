using Themia.Core.Files;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-13040] A fence's cells on its display, in its own DIPs: the title bar's height (whole rows of the widget grid) and
/// one icon box under it (a desktop icon's box on that grid), so what the fence draws stands on the desktop icon grid.
/// </summary>
public readonly record struct FenceCellsDip(double Title, SizeDip Box);

/// <summary>
/// [S-13000]..[S-13014] [S-13040]..[S-13043] What a fence widget asks of the host's desktop (<see cref="IDesktopItems.Fences"/>).
/// The fence's items are the desktop's own icons, laid by the host on cells of the desktop icon grid in the fence's body
/// (Core's <c>DesktopFences.Lay</c>): every gesture on one is a desktop icon's, the fence draws none of them. What is the
/// fence's alone (rolling up, scrolling, its title, its removal, drops of files into it, a paste or a new folder into it)
/// is asked here, by the fence's widget id, and carried out as one change of the state ([S-13010]: membership is undone
/// with the arrangement). A fence never edits its own items. UI thread.
/// </summary>
public interface IDesktopFences
{
    /// <summary>[S-13012] Roll the fence up to its title bar, or unroll it to the height it had. Not an undo step: a look, as Fences' own.</summary>
    void RollUp(string fenceId, bool rolledUp);

    /// <summary>[S-13043] Scroll the fence's body by <paramref name="rows"/> whole rows (down is positive), kept between its top and its last row. Not an undo step.</summary>
    void Scroll(string fenceId, int rows);

    /// <summary>[S-10910] The fence's title becomes <paramref name="name"/> (trimmed; empty is the kind's own word).</summary>
    void Rename(string fenceId, string name);

    /// <summary>
    /// [S-13009] Take the fence away, never a file: with <paramref name="putBack"/> its items stay on the free desktop of
    /// its view where it showed them (the ones it did not show at free spots nearest its corner), else their icons leave
    /// that view (Add widget lists them). One undo step that brings the fence back with its items.
    /// </summary>
    void Remove(string fenceId, bool putBack);

    /// <summary>
    /// [S-13041] Whether a drag of the desktop's own icons runs now through the shell (one that left the desktop and came
    /// back): over a fence the surface takes it as a drop on the desktop (the one drop computation), never the fence.
    /// </summary>
    bool DeskDragRunning { get; }

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
    /// [S-13040] The fence's title bar and one icon box on the display it is on, in DIPs: whole grid rows and a desktop
    /// icon's box on the widget grid. A picture's (no display): the title bar as the sizing states it (<c>FenceGrid.TitleDip</c>) and the desktop's plain cell.
    /// </summary>
    FenceCellsDip Cells(string fenceId);

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
}
