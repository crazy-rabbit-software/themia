using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Files;
using Windows.Foundation;

namespace Themia.Widget.Fence;

/// <summary>
/// [S-13005] Files from outside dropped on a fence (Explorer, a browser, an app): into the user's desktop folder through the
/// shell's own drop target (copy, move, link, the right-drag menu, virtual files), and they arrive in this fence. Never in
/// edit mode, never on a search fence. [S-13041] The desktop's own icons never drop here: they land on the fence's cells by
/// the desktop's one move and drop computation (the surface takes even a shell drag of them over the fence), and a drop over
/// one of the fence's folder icons is that icon's own, as on the free desktop.
/// </summary>
internal sealed partial class FenceView : IWidgetDropTarget
{
    DropEffect IWidgetDropTarget.DragOver(IWidgetDrag drag, Point local)
    {
        try
        {
            if (!Acts || WidgetId is null || Searches || drag.IsEmpty || Fences.DesktopFolder is not { } desktop)
            {
                return DropEffect.None;
            }

            drag.Place = _title.Text;
            return SurfaceDrops.Single(drag.ForwardOver(desktop, DropEffect.None));
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a drag over a fence failed", ex);
            return DropEffect.None;
        }
    }

    void IWidgetDropTarget.DragLeave()
    {
        // ignore: a fence shows nothing of a drag of files over it but the caption
    }

    DropEffect IWidgetDropTarget.Drop(IWidgetDrag drag, Point local)
    {
        try
        {
            return !Acts || WidgetId is not { } id || Searches || drag.IsEmpty ? DropEffect.None : Fences.DropFiles(id, drag);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a drop on a fence failed", ex);
            return DropEffect.None;
        }
    }
}
