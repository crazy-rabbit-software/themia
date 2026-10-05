using Microsoft.UI.Xaml.Controls.Primitives;
using Themia.App.Interop;
using Themia.App.Shell.Config;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Shell;

/// <summary>
/// Containers and the card hooks the shell answers ([S-866]..[S-875]): a card's drawn frame
/// (collapsed to its bar, hidden), layering, acting on a container's children, adding a widget inside
/// one, and dropping a widget onto one to put it inside.
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>
    /// [S-871] The cell a card is drawn in: its rect, or only its header bar while it is collapsed.
    /// The stored rect is never touched, so expanding gives the card exactly its old rect back.
    /// </summary>
    private PixelRect DrawnCell(WidgetCard card, FracRect rect, ScreenMetrics metrics)
    {
        var cell = GridGeometry.ToPixels(rect, metrics);
        return card.IsCollapsedToBar
            ? CardFrame.Collapsed(cell, card.HeaderEdge, WidgetCard.BarThickness + Math.Max(0, Design.WidgetGapPx))
            : cell;
    }

    /// <summary>The card showing widget <paramref name="widgetId"/> on this surface, if any.</summary>
    internal WidgetCard? CardFor(string widgetId)
        => _closed ? null : _cards.Find(c => string.Equals(c.Model.Id, widgetId, StringComparison.Ordinal));

    /// <inheritdoc />
    public void OnCardFrameChanged(WidgetCard card)
    {
        // A card still being built (its view collapsing itself during Attach) is positioned by the
        // relayout that is building it.
        if (_closed || !_cards.Contains(card) || IsArrangedByContainer(ViewForThisMonitor(_host.State), card.Model))
        {
            return;
        }

        PositionCard(card, card.Model.Rect);
        ApplyHitRegion();
    }

    /// <inheritdoc />
    public void RequestZOrder(WidgetCard card, bool front) => _host.ZOrderSelected(front);

    /// <inheritdoc />
    public void RequestChildAction(WidgetCard container, string childId, ContainerChildAction action)
    {
        switch (action)
        {
            case ContainerChildAction.Configure:
                _host.ShowConfig(new ConfigTarget.Widget(childId));
                return;

            case ContainerChildAction.MoveUp or ContainerChildAction.MoveDown:
                var delta = action == ContainerChildAction.MoveUp ? -1 : 1;
                _host.Mutate(state => ViewForThisMonitor(state).MoveChild(childId, delta), undo: UndoAction.Container);
                return;

            case ContainerChildAction.TakeOut:
                _host.Mutate(state =>
                {
                    if (!ViewForThisMonitor(state).Reparent(childId, null))
                    {
                        Log.Warn(Scope, $"refused to take widget '{childId}' out of '{container.Model.Id}'");
                    }
                }, undo: UndoAction.Container);
                return;
        }
    }

    /// <inheritdoc />
    public void RequestMoveChildTo(WidgetCard container, string childId, int index)
        => _host.Mutate(state => ViewForThisMonitor(state).MoveChildTo(childId, index), undo: UndoAction.Container);

    /// <summary>
    /// [S-870] The widget picker, anchored on the container, with the container chosen in its "Add
    /// to" box: selecting the container first is what makes the picker start there ([S-955]).
    /// </summary>
    public void RequestAddInside(WidgetCard container)
    {
        if (_closed)
        {
            return;
        }

        _host.Select(container.Model.Id);

        try
        {
            var flyout = BuildAddFlyout();
            flyout.ShowAt(container, new FlyoutShowOptions { Placement = FlyoutPlacementMode.Bottom });
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "could not open the widget picker for a container", ex);
        }
    }

    /// <summary>
    /// [S-875] A move that ends with the pointer over a container puts the widget inside it; one that
    /// ends outside the group it was in takes it out. Membership only — the widget stays where it was
    /// dropped (a group never moves its members, [S-411]); the rest is <see cref="ContainerRules.DropTarget"/>.
    /// Returns whether the drop changed anything (the mutation then persists the geometry too).
    /// </summary>
    private bool TryDropIntoContainer(WidgetCard card)
    {
        if (!Win32.GetCursorPos(out var cursor) || !_monitor.Contains(cursor.X, cursor.Y))
        {
            return false;
        }

        var view = ViewForThisMonitor(_host.State);
        if (IsArrangedByContainer(view, card.Model))
        {
            return false;
        }

        var (x, y) = FractionOf(cursor);
        if (view.DropTarget(card.Model.Id, x, y) is not { } target)
        {
            return false;
        }

        var id = card.Model.Id;
        var into = target.Length == 0 ? null : target;

        _host.Mutate(state =>
        {
            if (!ViewForThisMonitor(state).Reparent(id, into))
            {
                Log.Warn(Scope, $"refused to drop widget '{id}' into '{into ?? "the desktop"}'");
            }
        }, undo: UndoAction.Container);

        Log.Info(Scope, into is null ? $"widget '{id}' dropped out of its group" : $"widget '{id}' dropped into '{into}'");
        return true;
    }
}
