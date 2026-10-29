using Microsoft.UI.Xaml.Media;
using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-5510]..[S-5512] [S-5540]..[S-5549] The desktop's items as an item widget reads them
/// (<see cref="IWidgetHost.DesktopItems"/>): the host's one listing of the shell's desktop namespace,
/// kept current by the shell's change notifications, the items' icons as Explorer's desktop draws them,
/// and the one selection every item widget on every display shares, with the desktop's gestures
/// (press, keys, typing, the shell's menu, rename, drags). An item widget forwards its gestures here
/// and paints what it reads; the selection's rules, the namespace, the notifications, the image lists,
/// the shell's menus, file operations and OLE stay host code. UI thread throughout.
/// </summary>
public interface IDesktopItems
{
    /// <summary>The items as last read: empty until the first read, and while the desktop shows no item of Themia's.</summary>
    IReadOnlyList<DesktopItem> Items { get; }

    /// <summary>The listed item with <paramref name="key"/>, or null.</summary>
    DesktopItem? Find(string? key);

    /// <summary>Raised on the UI thread when the items, the selection, its look, a rename box or a drag's target changed.</summary>
    event Action? Changed;

    /// <summary>[S-5540] Whether <paramref name="key"/> is selected.</summary>
    bool IsSelected(string key);

    /// <summary>[S-6628] Whether <paramref name="key"/> draws the focus cue (focused, and the keys moved it there).</summary>
    bool ShowsFocus(string key);

    /// <summary>[S-6626] Whether the desktop has the keyboard: a selection is drawn active, else inactive.</summary>
    bool Active { get; }

    /// <summary>[S-5570] Whether <paramref name="key"/> is the focused item while the desktop has the keyboard: what a screen reader is told has the focus.</summary>
    bool IsFocused(string key);

    /// <summary>[S-5570] <paramref name="key"/>'s place in the desktop's order and the count ("3 of 12"), or null when it is not shown.</summary>
    (int Position, int Count)? PositionOf(string key);

    /// <summary>[S-5545] Whether <paramref name="key"/> is cut and still on the clipboard: drawn faded, as Explorer does.</summary>
    bool IsCut(string key);

    /// <summary>[S-5543] The widget (its id) whose item shows its rename box now, or null.</summary>
    string? RenamingWidget { get; }

    /// <summary>[S-5549] The item widget a drag hovers as its drop target now (its id), or null.</summary>
    string? DropTargetWidget { get; }

    /// <summary>[S-5540] A left press on <paramref name="key"/>'s widget outside edit mode (Ctrl toggles, Shift selects the range).</summary>
    void Press(string key, bool ctrl, bool shift);

    /// <summary>[S-6621] The release of a press that was no drag.</summary>
    void Release(string key);

    /// <summary>
    /// [S-5548] A right click on <paramref name="key"/>: it joins the selection as Explorer's does, and the
    /// shell's own menu for the selection opens at the screen point (physical pixels) over <paramref name="owner"/>.
    /// </summary>
    void Context(string key, int screenX, int screenY, nint owner);

    /// <summary>[S-5541] [S-5545] A key with the keyboard lease, from the widget <paramref name="widgetId"/>: true when the desktop used it.</summary>
    bool Key(WidgetKey key, bool shift, string? widgetId);

    /// <summary>[S-5542] A character typed with the lease: type-to-find. True when it found an item.</summary>
    bool Type(char character);

    /// <summary>[S-5530] A double-click on <paramref name="key"/>: the selection opens (the item alone when it is not selected).</summary>
    void Open(string key);

    /// <summary>
    /// [S-5549] The pointer left the dead zone with the left button held on <paramref name="key"/>: the
    /// selection is dragged out as the shell's data object from <paramref name="window"/> (posted by the
    /// caller, never run inside its pointer handler); within the desktop the drop moves the widgets.
    /// </summary>
    void DragOut(string key, nint window);

    /// <summary>[S-5549] Whether <paramref name="key"/> is in the drag that is running from the desktop now (it takes no drop of itself).</summary>
    bool Dragging(string key);

    /// <summary>[S-5549] The widget <paramref name="widgetId"/> is a drag's target now (or no longer, null): every item widget redraws.</summary>
    void SetDropTarget(string? widgetId);

    /// <summary>
    /// [S-5543] The rename box's text is to be committed: null when the box may close (renamed through the
    /// shell's own operation, or unchanged), else the sentence saying why it stays.
    /// </summary>
    string? CommitRename(string key, string text);

    /// <summary>[S-5543] The rename box closes without a rename (Escape).</summary>
    void CancelRename();

    /// <summary>
    /// [S-5570] Rename <paramref name="key"/> to <paramref name="name"/> without the box (a screen reader's
    /// value pattern), by F2's rules ([S-5543]): null when it was handed to the shell's rename (or
    /// unchanged), else the sentence saying why not.
    /// </summary>
    string? RenameTo(string key, string name);

    /// <summary>[S-5512] The cached icon for <paramref name="item"/> at <paramref name="pixels"/> (physical), if there is one (no extraction).</summary>
    bool TryIcon(DesktopItem item, int pixels, out ImageSource? image);

    /// <summary>[S-5512] The shell's icon with its overlay (the shortcut arrow among them), or null.</summary>
    Task<ImageSource?> IconAsync(DesktopItem item, int pixels);
}
