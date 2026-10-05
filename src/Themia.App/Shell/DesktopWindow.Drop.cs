using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Themia.App.Interop;
using Themia.App.Services.Files;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Files;
using Themia.Core.Widgets;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>
/// [S-1020]..[S-1024] The surface as the one OLE drop target for files, folders and links dragged
/// from Explorer, Outlook, a browser or another widget.
/// <para>
/// Where a drop can land follows the hit region ([S-851]): outside edit mode the surface exists only
/// where widgets (and the view bar, and notices) are, so a drag between widgets is over Explorer's
/// own desktop and never reaches us — Explorer takes it as it always does. Over a widget the view
/// under the pointer decides (<see cref="IWidgetDropTarget"/>); a widget without drops refuses. In edit
/// mode the whole work area is ours, widgets take no drops, and bare surface stands in for the
/// desktop it covers: the drop goes to the Desktop folder through the shell's own drop target, so it
/// does just what dropping on the desktop would (same-volume move, cross-volume copy, links, virtual
/// files, conflict UI).
/// </para>
/// </summary>
internal sealed partial class DesktopWindow : DragDropInterop.IOleDropHandler
{
    private IDisposable? _dropRegistration;
    private DragDropInterop.ComRef _dropHelper;

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
    }

    /// <summary>Revoke the drop target before the window goes; a drag in flight is left first.</summary>
    private void DetachDropTarget()
    {
        EndDrag();
        _dropRegistration?.Dispose();
        _dropRegistration = null;
        _dropHelper.Dispose();
    }

    // ---- IOleDropHandler ------------------------------------------------------------------------

    public DropEffect Enter(nint data, uint keys, int screenX, int screenY, DropEffect allowed)
    {
        // A drag that never left (the source died mid-drag) is ended before the new one starts.
        EndDrag();

        _drag = new WidgetDrag(data, keys, screenX, screenY, allowed);
        var effect = Hover(_drag);

        if (!_dropHelper.IsNull)
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

        if (!_dropHelper.IsNull)
        {
            DragDropInterop.HelperOver(_dropHelper.Pointer, screenX, screenY, effect);
        }

        return effect;
    }

    public void Leave()
    {
        if (!_dropHelper.IsNull)
        {
            DragDropInterop.HelperLeave(_dropHelper.Pointer);
        }

        EndDrag();
    }

    public DropEffect Drop(nint data, uint keys, int screenX, int screenY, DropEffect allowed)
    {
        var drag = _drag ?? new WidgetDrag(data, keys, screenX, screenY, allowed);
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
            if (!_dropHelper.IsNull)
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
        var (route, view, local) = Resolve(drag);
        Switch(drag, route, view);
        drag.Place = null;

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
        var (route, view, local) = Resolve(drag);
        Switch(drag, route, view);

        var effect = route switch
        {
            DropRoute.Widget when view is not null => view.Drop(drag, local),
            DropRoute.Desktop when DesktopFolder() is { } desktop => drag.ForwardDrop(desktop, DropEffect.None),
            _ => DropEffect.None,
        };

        Log.Debug(Scope, $"drop on {_monitor.GdiName}: {route} -> {effect}");
        return effect;
    }

    /// <summary>Who takes the drag at its current point, and that point in the taker's own DIPs.</summary>
    private (DropRoute Route, IWidgetDropTarget? View, Point Local) Resolve(WidgetDrag drag)
    {
        var point = ToContent(drag.ScreenX, drag.ScreenY);
        var (hit, card) = DropHitAt(point);
        var view = card?.View as IWidgetDropTarget;
        var route = SurfaceDrops.Route(hit, EditMode, view is not null, drag.Content);

        if (route != DropRoute.Widget || card is null || view is null)
        {
            return (route, null, point);
        }

        // The view's own DIPs: through the card's content scale ([S-850]) and wherever it sits.
        return (route, view, _root.TransformToVisual(card.View).TransformPoint(point));
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
        var origin = default(Win32.Point);

        if (!Win32.ClientToScreen(_hwnd, ref origin))
        {
            origin = new Win32.Point { X = _monitor.Bounds.Left, Y = _monitor.Bounds.Top };
        }

        var scale = RasterScale;
        return new Point((screenX - origin.X) / scale, (screenY - origin.Y) / scale);
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
