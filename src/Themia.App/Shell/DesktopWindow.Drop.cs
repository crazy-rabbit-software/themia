using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Themia.App.Interop;
using Themia.App.Services.Files;
using Themia.App.Widgets;
using Themia.Core.Desktop;
using Themia.Core.Diagnostics;
using Themia.Core.Files;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>
/// [S-1020]..[S-1024] The surface as the one OLE drop target for files, folders and links dragged
/// from Explorer, Outlook, a browser or another widget.
/// <para>
/// [S-1021] The surface is its display's whole work area ([S-851]), in edit mode and outside it, so
/// every drag over the desktop reaches us. Over a widget the view under the pointer decides
/// (<see cref="IWidgetDropTarget"/>); a widget without drops refuses, and in edit mode widgets take
/// no drops. Bare surface is the desktop: the drop goes to the Desktop folder through the shell's own
/// drop target, so it does just what dropping on Explorer's desktop would (same-volume move,
/// cross-volume copy, links, virtual files, conflict UI).
/// </para>
/// </summary>
internal sealed partial class DesktopWindow : DragDropInterop.IOleDropHandler
{
    private IDisposable? _dropRegistration;
    private ComPtr _dropHelper;

    /// <summary>The drag over this surface right now, from entry to its drop or its leaving.</summary>
    private WidgetDrag? _drag;

    private DropRoute _dropRoute;
    private IWidgetDropTarget? _dropView;
    private DropImage _dropShown = DropImage.Invalid;

    /// <summary>
    /// [S-1021] Take the surface's windows as a drop target once its content is live (the content
    /// bridge and its input site are child windows XAML makes, and the target has to be on them too).
    /// </summary>
    private void OnRootLoadedForDrops(object sender, RoutedEventArgs e)
    {
        if (_dropRegistration is not null || _closed)
        {
            return;
        }

        _dropRegistration = DragDropInterop.RegisterDropTarget(_hwnd, this);

        if (_dropRegistration is null)
        {
            Log.Warn(Scope, $"{_monitor.GdiName}: no drop target; files cannot be dropped on its widgets");
            return;
        }

        _dropHelper = DragDropInterop.CreateDropHelper();
        AttachOwnDrags();
    }

    // ---- our own drags ([S-5549]) ------------------------------------------------------------------

    private bool _ownDragsAttached;

    /// <summary>
    /// [S-5549] A drag one of our own views started (<see cref="ItemDrag"/>) is XAML's, and XAML routes
    /// a drag of its own process to its drop events, never to the OLE target above (seen live: the drag
    /// ran and ended with nothing, the target unheard). So the surface's root takes those events and
    /// feeds the same routing; a drag from elsewhere still arrives through OLE and is left alone here.
    /// </summary>
    private void AttachOwnDrags()
    {
        if (_ownDragsAttached)
        {
            return;
        }

        _ownDragsAttached = true;
        _root.AllowDrop = true;
        _root.DragOver += OnOwnDragOver;
        _root.Drop += OnOwnDrop;
        _root.DragLeave += OnOwnDragLeave;
    }

    private void DetachOwnDrags()
    {
        if (!_ownDragsAttached)
        {
            return;
        }

        _ownDragsAttached = false;
        _root.DragOver -= OnOwnDragOver;
        _root.Drop -= OnOwnDrop;
        _root.DragLeave -= OnOwnDragLeave;
    }

    /// <summary>The OLE key flags of XAML's modifiers: the left button, Shift and Ctrl.</summary>
    private static uint OwnKeys(Windows.ApplicationModel.DataTransfer.DragDrop.DragDropModifiers modifiers)
        => 0x1u
            | (modifiers.HasFlag(Windows.ApplicationModel.DataTransfer.DragDrop.DragDropModifiers.Shift) ? 0x4u : 0)
            | (modifiers.HasFlag(Windows.ApplicationModel.DataTransfer.DragDrop.DragDropModifiers.Control) ? 0x8u : 0);

    private void OnOwnDragOver(object sender, DragEventArgs e)
    {
        try
        {
            if (ItemDrag.Running is null || !Win32.GetCursorPos(out var at))
            {
                return;
            }

            var allowed = (DropEffect)(uint)e.AllowedOperations;
            var keys = OwnKeys(e.Modifiers);
            var effect = _drag is null ? Enter(0, keys, at.X, at.Y, allowed) : Over(keys, at.X, at.Y, allowed);
            e.AcceptedOperation = (Windows.ApplicationModel.DataTransfer.DataPackageOperation)(uint)effect;

            // [S-5546] Icons that only move on the desktop show their picture and nothing else, as Explorer's.
            var moves = _drag is { } drag && Resolve(drag).Moves;
            // [S-13026] The drag's picture is the surface's own here, under the pointer wherever it is (a fence's ghosts beside it).
            e.DragUIOverride.IsContentVisible = !ShowDragPicture(ItemDrag.RunningPicture, ToContent(at.X, at.Y));
            e.DragUIOverride.IsGlyphVisible = !moves;
            e.DragUIOverride.IsCaptionVisible = !moves && _drag?.Place is not null;
            if (!moves && _drag?.Place is { } place)
            {
                e.DragUIOverride.Caption = place;
            }

            e.Handled = true;
        }
        catch (Exception ex)
        {
            // XAML's event: nothing may escape into its drag.
            Log.Error(Scope, "routing one of our own drags over the surface failed", ex);
        }
    }

    private void OnOwnDrop(object sender, DragEventArgs e)
    {
        try
        {
            if (ItemDrag.Running is null || !Win32.GetCursorPos(out var at))
            {
                return;
            }

            HideDragPicture();
            var effect = Drop(0, OwnKeys(e.Modifiers), at.X, at.Y, (DropEffect)(uint)e.AllowedOperations);
            e.AcceptedOperation = (Windows.ApplicationModel.DataTransfer.DataPackageOperation)(uint)effect;
            e.Handled = true;
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "dropping one of our own drags on the surface failed", ex);
        }
    }

    private void OnOwnDragLeave(object sender, DragEventArgs e)
    {
        HideDragPicture();
        if (ItemDrag.Running is not null && _drag is { Data: 0 })
        {
            Leave();
        }
    }

    /// <summary>Revoke the drop target before the window goes; a drag in flight is left first.</summary>
    private void DetachDropTarget()
    {
        EndDrag();
        DetachOwnDrags();
        _dropRegistration?.Dispose();
        _dropRegistration = null;
        _dropHelper.Dispose();
    }

    // ---- IOleDropHandler ------------------------------------------------------------------------

    public DropEffect Enter(nint data, uint keys, int screenX, int screenY, DropEffect allowed)
    {
        // A drag that never left (the source died mid-drag) is ended before the new one starts.
        EndDrag();

        // A drag a view of ours started is read from its offer, never from its data object ([S-1022]).
        _drag = new WidgetDrag(data, keys, screenX, screenY, allowed, ItemDrag.Running);
        var effect = Hover(_drag);

        if (!_dropHelper.IsNull && data != 0)
        {
            DragDropInterop.HelperEnter(_dropHelper.Pointer, _hwnd, data, screenX, screenY, effect);
        }

        return effect;
    }

    public DropEffect Over(uint keys, int screenX, int screenY, DropEffect allowed)
    {
        if (_drag is not { } drag)
        {
            return DropEffect.None;
        }

        drag.Update(keys, screenX, screenY, allowed);
        var effect = Hover(drag);

        if (!_dropHelper.IsNull && drag.Data != 0)
        {
            DragDropInterop.HelperOver(_dropHelper.Pointer, screenX, screenY, effect);
        }

        return effect;
    }

    public void Leave()
    {
        if (!_dropHelper.IsNull && _drag is { Data: not 0 })
        {
            DragDropInterop.HelperLeave(_dropHelper.Pointer);
        }

        EndDrag();
    }

    public DropEffect Drop(nint data, uint keys, int screenX, int screenY, DropEffect allowed)
    {
        var drag = _drag ?? new WidgetDrag(data, keys, screenX, screenY, allowed, ItemDrag.Running);
        _drag = drag;
        drag.Update(keys, screenX, screenY, allowed);

        var effect = DropEffect.None;

        try
        {
            // The caption goes before the drop: the shell's target may run a conflict dialog, and the
            // source's drag image must not keep saying "Move to …" over it.
            Describe(drag, DropFeedback.Refused);
            effect = Deliver(drag);
        }
        finally
        {
            if (!_dropHelper.IsNull && data != 0)
            {
                DragDropInterop.HelperDrop(_dropHelper.Pointer, data, screenX, screenY, effect);
            }

            EndDrag();
        }

        return effect;
    }

    // ---- routing --------------------------------------------------------------------------------

    /// <summary>Route the drag where it is now and answer the effect it shows.</summary>
    private DropEffect Hover(WidgetDrag drag)
    {
        var (route, view, local, moves) = Resolve(drag);
        Switch(drag, route, view);
        drag.Place = null;

        // [S-5546] The desktop's own icons where they would move: taken, with no mark and no caption.
        if (moves)
        {
            var move = DesktopCommands.MoveEffect(drag.Allowed);
            Describe(drag, DropFeedback.Plain(move));
            return move;
        }

        var effect = route switch
        {
            DropRoute.Widget when view is not null => SurfaceDrops.Single(view.DragOver(drag, local)),
            DropRoute.Desktop when DesktopFolder() is { } desktop => HoverDesktop(drag, desktop),
            _ => DropEffect.None,
        };

        Describe(drag, drag.Place is { } place ? SurfaceDrops.Feedback(effect, place) : DropFeedback.Refused);
        return effect;
    }

    /// <summary>Bare surface: what the Desktop folder's own shell target answers.</summary>
    private static DropEffect HoverDesktop(WidgetDrag drag, string desktop)
    {
        drag.Place = FolderPath.DisplayName(desktop);
        return SurfaceDrops.Single(drag.ForwardOver(desktop, DropEffect.None));
    }

    /// <summary>[S-1022] Carry the drop out where the pointer is.</summary>
    private DropEffect Deliver(WidgetDrag drag)
    {
        var (route, view, local, moves) = Resolve(drag);
        Switch(drag, route, view);

        var effect = moves ? MoveDeskItems(drag, local) : route switch
        {
            DropRoute.Widget when view is not null => view.Drop(drag, local),
            DropRoute.Desktop when DesktopFolder() is { } desktop => DropOnDesktop(drag, desktop, local),
            _ => DropEffect.None,
        };

        Log.Debug(Scope, $"drop on {_monitor.GdiName}: {route} -> {effect}");
        return effect;
    }

    /// <summary>[S-5546] The desktop's own drag let go over empty desktop here: its icons' widgets move under the pointer, and no file does.</summary>
    private DropEffect MoveDeskItems(WidgetDrag drag, Point at)
    {
        _host.MoveDeskDrag(this, at);
        return DesktopCommands.MoveEffect(drag.Allowed);
    }

    /// <summary>
    /// [S-5591] A drop on bare surface goes into the desktop folder through the shell's own target; while
    /// what it brings lands here, in this display's view, at the drop point.
    /// </summary>
    private DropEffect DropOnDesktop(WidgetDrag drag, string desktop, Point at)
    {
        _host.ExpectDeskDrop(this, at, Services.AppHost.DropNames(drag));
        var effect = drag.ForwardDrop(desktop, DropEffect.None);

        // [S-13102] What the shell did is one step of the undo log once the files arrive.
        _host.NoteDropUndo(effect, drag.Paths);
        return effect;
    }

    /// <summary>
    /// Who takes the drag at its current point, and that point in the taker's own DIPs. [S-5546]
    /// <c>Moves</c>: the drag is the desktop's own and here its icons move (empty desktop, or one of the
    /// dragged icons themselves), whatever it carries; that goes before every other route.
    /// </summary>
    private (DropRoute Route, IWidgetDropTarget? View, Point Local, bool Moves) Resolve(WidgetDrag drag)
    {
        var point = ToContent(drag.ScreenX, drag.ScreenY);
        var (hit, card) = DropHitAt(point);
        // [S-13041] Over a fence (its icons too, [S-13047]) the desktop's own icons land by the one drop computation: on its cells.
        if (DesktopCommands.MovesIcons(_host.DeskDragRunning, EditMode, hit, card is not null
            && (_host.DeskDragHolds(card.Model.Id) || Services.AppHost.FenceTakesDeskDrag(ViewForThisMonitor(_host.State), card.Model.Id))))
        {
            return (DropRoute.Desktop, null, point, true);
        }

        var view = card?.View as IWidgetDropTarget;
        var route = SurfaceDrops.Route(hit, EditMode, view is not null, drag.Content);

        if (route != DropRoute.Widget || card is null || view is null)
        {
            return (route, null, point, false);
        }

        // The view's own DIPs: through the card's content scale ([S-850]) and wherever it sits.
        return (route, view, _root.TransformToVisual(card.View).TransformPoint(point), false);
    }

    /// <summary>
    /// [S-5549] [S-13046] [S-13048] The picture a move of the desktop's icons shows under the pointer: the held icon alone
    /// (<paramref name="held"/>, its card), whatever the number of icons moving (<paramref name="moving"/>): never the others
    /// stacked behind it, never their count (<see cref="DragGhost.OfMove"/>); no name, no highlight, nothing in its desktop
    /// place. The pointer (<paramref name="pointer"/>, the root's DIPs: the move's press) holds it where it grabbed it. Composed
    /// at once from the picture the icon's view keeps (<see cref="IDesktopIconView.DragIcon"/>), at this display's scale:
    /// nothing is rendered here. [S-13047] The shell's drag the move becomes shows this same picture (<see cref="HandedMove"/>).
    /// Null when the held icon has no picture yet; the drag then shows the pressed icon as it is drawn.
    /// </summary>
    internal DragPicture? DeskDragPicture(WidgetCard held, int moving, Point pointer)
    {
        ArgumentNullException.ThrowIfNull(held);
        try
        {
            if (held.View is not IDesktopIconView icon || icon.DragIcon is not { } picture
                || icon.DragIconBox is not { ActualWidth: > 0, ActualHeight: > 0 } box)
            {
                return null;
            }

            // The icon's square on the surface: through the card's content scale ([S-850]) and wherever it sits.
            var square = box.TransformToVisual(_root).TransformBounds(new Rect(0, 0, box.ActualWidth, box.ActualHeight));
            return DragGhost.OfMove(picture, moving, square.Width, pointer.X - square.X, pointer.Y - square.Y, RasterScale);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "the picture of the moving desktop icon could not be drawn; the drag shows the pressed icon as drawn", ex);
            return null;
        }
    }

    /// <summary>Leave the previous taker when the drag moved on to another.</summary>
    private void Switch(WidgetDrag drag, DropRoute route, IWidgetDropTarget? view)
    {
        if (route == _dropRoute && ReferenceEquals(view, _dropView))
        {
            return;
        }

        _dropView?.DragLeave();
        drag.EndForward();
        _dropRoute = route;
        _dropView = view;
    }

    /// <summary>The drag is over (left, dropped, or the surface is closing): clear every trace of it.</summary>
    private void EndDrag()
    {
        HideDragPicture();
        if (_drag is not { } drag)
        {
            return;
        }

        try
        {
            _dropView?.DragLeave();
            Describe(drag, DropFeedback.Refused);
        }
        finally
        {
            _dropView = null;
            _dropRoute = DropRoute.Refuse;
            _drag = null;
            drag.Dispose();
        }
    }

    /// <summary>
    /// [S-1024] The caption beside the drag image. Re-sent on every hover while it says something (a
    /// folder's shell target may write its own between our calls); cleared once when it stops.
    /// </summary>
    private void Describe(WidgetDrag drag, DropFeedback feedback)
    {
        if (drag.Data == 0 || (feedback.Image == DropImage.Invalid && _dropShown == DropImage.Invalid))
        {
            return;
        }

        DragDropInterop.SetDescription(drag.Data, feedback);
        _dropShown = feedback.Image;
    }

    /// <summary>
    /// [S-1020] What the drag is over: the widget card under it, the surface's own controls, or bare
    /// surface.
    /// </summary>
    private (SurfaceHit Hit, WidgetCard? Card) DropHitAt(Point point)
    {
        // Topmost first: the first element is the leaf actually under the pointer.
        foreach (var element in VisualTreeHelper.FindElementsInHostCoordinates(point, _root))
        {
            if (ReferenceEquals(element, _root) || ReferenceEquals(element, _widgetLayer) || ReferenceEquals(element, _barLayer) || ReferenceEquals(element, _ghostLayer))
            {
                return (SurfaceHit.Empty, null);
            }

            for (DependencyObject? at = element; at is not null; at = VisualTreeHelper.GetParent(at))
            {
                if (at is WidgetCard card)
                {
                    return (SurfaceHit.Widget, card);
                }
            }

            return (SurfaceHit.Chrome, null);
        }

        return (SurfaceHit.Empty, null);
    }

    /// <summary>A physical screen point in the root's DIPs.</summary>
    private Point ToContent(int screenX, int screenY)
    {
        var origin = ScreenOrigin();
        var scale = RasterScale;
        return new Point((screenX - origin.X) / scale, (screenY - origin.Y) / scale);
    }

    /// <summary>[S-13047] A point in the root's DIPs as a physical screen point: <see cref="ToContent"/> the other way.</summary>
    private Win32.Point ToScreen(Point dip)
    {
        var origin = ScreenOrigin();
        var scale = RasterScale;
        return new Win32.Point { X = origin.X + (int)Math.Round(dip.X * scale), Y = origin.Y + (int)Math.Round(dip.Y * scale) };
    }

    /// <summary>Where the root's top left is on the screen, in physical pixels.</summary>
    private Win32.Point ScreenOrigin()
    {
        var origin = default(Win32.Point);
        if (!Win32.ClientToScreen(_hwnd, ref origin))
        {
            origin = new Win32.Point { X = _monitor.Bounds.Left, Y = _monitor.Bounds.Top };
        }

        return origin;
    }

    /// <summary>The user's Desktop folder (wherever it is redirected), or null (logged) when Windows names none.</summary>
    private static string? DesktopFolder()
    {
        var path = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        if (string.IsNullOrEmpty(path))
        {
            Log.Warn(Scope, "Windows reports no Desktop folder; drops on bare surface are refused");
            return null;
        }

        return path;
    }
}
