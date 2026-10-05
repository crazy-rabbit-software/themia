namespace Themia.Core.Model;

/// <summary>Whether a clipboard entry duplicates its widgets or moves them.</summary>
public enum ClipboardMode
{
    Copy = 0,
    Cut = 1,
}

/// <summary>
/// Copy / cut for widgets, across views and across displays. Paste does not happen here: it starts a
/// placement session carrying <see cref="Payload"/> ([S-74], [S-1589]).
/// <para>
/// In-process rather than on the system clipboard ([S-71]): the payload is a live widget
/// configuration, not text, and putting it on the system clipboard would both clobber whatever the
/// user had there and invite other apps to paste something that is not a widget.
/// </para>
/// <para>
/// A cut is a <b>mark</b>, not an immediate removal ([S-73]) — the originals stay put until a paste
/// actually lands, so cutting and then changing your mind costs nothing.
/// </para>
/// </summary>
public sealed class WidgetClipboard
{
    /// <summary>The snapshot a paste places: a copy for Copy, a move for Cut. Null when empty.</summary>
    public PlacementPayload? Payload { get; private set; }

    public ClipboardMode Mode { get; private set; }

    public bool HasContent => Payload is not null;

    /// <summary>Raised whenever the contents change, so menus can enable or disable Paste.</summary>
    public event Action? Changed;

    /// <summary>
    /// [S-72]/[S-1588] Snapshot the picked widgets of one view. Returns false, leaving the clipboard as it was, when none of them is in the view.
    /// </summary>
    public bool Copy(ViewState view, IEnumerable<string> ids) => Set(view, ids, ClipboardMode.Copy);

    /// <summary>[S-73] As <see cref="Copy"/>, but the paste moves the originals instead of copying them.</summary>
    public bool Cut(ViewState view, IEnumerable<string> ids) => Set(view, ids, ClipboardMode.Cut);

    private bool Set(ViewState view, IEnumerable<string> ids, ClipboardMode mode)
    {
        var payload = PlacementPayload.FromView(view, ids, mode == ClipboardMode.Cut ? PlacementMode.Move : PlacementMode.Copy);
        if (payload is null)
        {
            return false;
        }

        Payload = payload;
        Mode = mode;
        Changed?.Invoke();
        return true;
    }

    public void Clear()
    {
        if (Payload is null)
        {
            return;
        }

        Payload = null;
        Changed?.Invoke();
    }

    /// <summary>
    /// [S-73] A placement landed. If it was this clipboard's cut, the cut is consumed — placing it
    /// twice would mean moving the same widgets to two places — while a copy stays available for
    /// repeat pastes. A placement that was cancelled never gets here, so a cancelled paste keeps the cut.
    /// </summary>
    public void OnPlaced(PlacementPayload placed)
    {
        if (Mode == ClipboardMode.Cut && ReferenceEquals(placed, Payload))
        {
            Clear();
        }
    }
}
