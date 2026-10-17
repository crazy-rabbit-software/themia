using Themia.Core.Files;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1400]/[S-1020] Implemented by a view that takes things dropped onto it from Explorer, a browser or the
/// Start menu (the folder and Shortcuts widgets).
/// <para>
/// There is <b>one</b> drop path in the app: the desktop surface registers the only OLE drop target
/// (<c>DragDropInterop.RegisterDropTarget</c> on its windows, [S-1021]), hit-tests the cursor to the
/// innermost card, and forwards here with the point in this view's own DIPs (<c>DesktopWindow.Drop.cs</c>).
/// A view never registers a target of its own and never uses XAML's <c>AllowDrop</c>. Never called
/// in edit mode; a view that does not implement this refuses drops ([S-1020]).
/// </para>
/// <para>
/// The member names are UIElement's event names, so a view implements them <b>explicitly</b>
/// (<c>DropEffect IWidgetDropTarget.DragOver(...)</c>) rather than hiding the events.
/// </para>
/// <para>
/// UI thread. Never throws into the caller (OLE is on the stack): a view logs and answers None.
/// </para>
/// </summary>
public interface IWidgetDropTarget
{
    /// <summary>
    /// The drag entered or moved over this view. Show where it would land, name the place for the
    /// caption (<see cref="IWidgetDrag.Place"/>), and return what it would do (None: refused here; the
    /// surface does not pass it on to the desktop behind the widget).
    /// </summary>
    DropEffect DragOver(IWidgetDrag drag, Point local);

    /// <summary>The drag left this view, or was cancelled: take any highlight down.</summary>
    void DragLeave();

    /// <summary>The drop. Returns what was done (None when nothing was taken).</summary>
    DropEffect Drop(IWidgetDrag drag, Point local);
}

/// <summary>
/// [S-1022] One drag over a desktop surface as a view sees it inside the <see cref="IWidgetDropTarget"/>
/// calls: what it carries (read once, on entry), the keys and effects as they change, and the
/// forwarding to a folder's shell drop target. The OLE side is the host's; a view never holds one
/// past the call.
/// </summary>
public interface IWidgetDrag
{
    /// <summary>The source's <c>IDataObject</c>, for what the summary does not carry. Valid inside the view calls; 0 once the drag ended.</summary>
    nint Data { get; }

    /// <summary>File-system paths (<c>CF_HDROP</c>, or a Chromium app's <c>file:///</c> lines), in the source's order.</summary>
    IReadOnlyList<string> Paths { get; }

    /// <summary>Web addresses, only read when no file came with the drag.</summary>
    IReadOnlyList<string> Urls { get; }

    /// <summary>How many "virtual" files it offers (Outlook attachments).</summary>
    int VirtualFiles { get; }

    /// <summary>The virtual files' relative names.</summary>
    IReadOnlyList<string> VirtualFileNames { get; }

    /// <summary>The drag carries shell items (every Explorer drag, and a Start-menu app, which has nothing else).</summary>
    bool HasShellItems { get; }

    /// <summary>How many things the drag carries, for logs.</summary>
    int ItemCount { get; }

    /// <summary>Whether the drag carries anything at all a widget or the desktop could take.</summary>
    bool IsEmpty { get; }

    DropContent Content { get; }

    /// <summary>The raw <c>MK_*</c> state of the last callback.</summary>
    uint Keys { get; }

    /// <summary>Ctrl held (copy rather than move, where that means something).</summary>
    bool Ctrl { get; }

    bool Shift { get; }

    /// <summary>The effects the source allows.</summary>
    DropEffect Allowed { get; }

    int ScreenX { get; }

    int ScreenY { get; }

    /// <summary>[S-1024] Where a drop would land, as the caption beside the drag image names it. Set in <see cref="IWidgetDropTarget.DragOver"/>.</summary>
    string? Place { get; set; }

    /// <summary>[S-1022] Hover over <paramref name="folder"/>: the effect its shell drop target answers.</summary>
    DropEffect ForwardOver(string folder, DropEffect forced);

    /// <summary>[S-1022] Drop into <paramref name="folder"/> through its shell drop target. Returns what it did.</summary>
    DropEffect ForwardDrop(string folder, DropEffect forced);

    /// <summary>Leave the folder target, if one is entered.</summary>
    void EndForward();

    /// <summary>Save the drag's virtual files under <paramref name="destination"/>. Returns the paths written.</summary>
    IReadOnlyList<string> SaveVirtualFiles(string destination);
}
