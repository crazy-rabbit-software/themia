using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>
/// [S-1580]..[S-1586], [S-1591] Multi-select on this display: the rubber band, Ctrl+A, and dragging
/// the whole selection as one block — clamped and snapped together, and handed to another display
/// together. [S-2008] [S-11879] The same move on the locked desktop, where the selection is the desktop's
/// one (icons and cards, <see cref="Themia.Core.Desktop.DesktopPicks"/>) and the local grid shows where
/// the block lands ([S-11874]).
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>A move of one or more cards that started on this surface.</summary>
    private sealed class MoveSession(WidgetCard anchor, List<(WidgetCard Card, FracRect Start)> cards, PlacementPayload? payload, BlockMove block)
    {
        /// <summary>The card the pointer holds.</summary>
        public WidgetCard Anchor { get; } = anchor;

        /// <summary>Every card that moves, with the rect it started at.</summary>
        public List<(WidgetCard Card, FracRect Start)> Cards { get; } = cards;

        /// <summary>[S-1591] The same widgets as a move anchored on the held card, for a hand-off to another display.</summary>
        public PlacementPayload? Payload { get; } = payload;

        /// <summary>[S-2244] The cards have moved: they are free of their neighbours until dropped.</summary>
        public bool Moved { get; set; }

        /// <summary>[S-11890] [S-11891] Where the block is drawn and lands: on the grid.</summary>
        public BlockMove Block { get; } = block;

        /// <summary>[S-11890] The pointer's moves since the last frame, the latest kept.</summary>
        public PointerFrames Frames { get; } = new();

        /// <summary>[S-78] Whether the last step showed the block going to another display.</summary>
        public bool Leaving { get; set; }

        /// <summary>[S-13046] Whether a desktop icon holds the move: its icons stand where they land as their ghosts.</summary>
        public bool HeldByIcon { get; init; }

        /// <summary>[S-13046] [S-13048] The held icon's picture alone, under the pointer while a desktop icon holds the move; else null.</summary>
        public Themia.Core.Files.DragPicture? Picture { get; init; }

        /// <summary>[S-13046] Where the pointer pressed, in the root's DIPs: the picture follows it by the move's translation.</summary>
        public Point Press { get; init; }

        /// <summary>
        /// [S-13713] The desktop items the move takes off the view if let go over the open Unplaced icons page
        /// (<see cref="Themia.Core.Desktop.UnplacedItems.Taken"/>); none for a move a desktop icon does not hold.
        /// </summary>
        public IReadOnlyList<string> Takes { get; init; } = [];

        /// <summary>[S-13713] Whether the last step had the pointer over the open Unplaced icons page with icons it takes.</summary>
        public bool OverPage { get; set; }
    }

    private MoveSession? _move;

    /// <summary>
    /// [S-13047] The move that became the shell's drag ([S-11878]): where it was pressed (physical screen pixels) and the picture
    /// it showed under the pointer. The drag shows that picture, held where the icon was grabbed, and lands the icons by that
    /// grab, so nothing under the pointer changes as the move hands over (owner, 2026-11-20: "same preview under my mouse").
    /// Null until a move hands over; a new move clears it.
    /// </summary>
    internal (Interop.Win32.Point Press, Themia.Core.Files.DragPicture? Picture)? HandedMove { get; private set; }

    /// <summary>
    /// [S-11877] Whether the widget <paramref name="widgetId"/> is drawn on this surface now (a card that hides itself, [S-873],
    /// is not; [S-13042] nor an icon its fence does not show).
    /// </summary>
    internal bool Draws(string widgetId) => CardFor(widgetId) is not { } card || (card.Visibility == Visibility.Visible && card.IsShown(EditMode));

    /// <summary>
    /// [S-11877] The desktop's one selection changed: every card on the locked desktop draws whether it is in it;
    /// [S-14310] several cards selected here, the frame's corners round them instead of theirs.
    /// </summary>
    internal void RepaintDeskSelection()
    {
        if (_closed || EditMode)
        {
            return;
        }

        foreach (var card in _cards)
        {
            card.ApplySelection();
        }

        SyncFrame();
    }

    /// <summary>The ids of the cards on this surface that are collapsed, so draw nothing.</summary>
    private HashSet<string> HiddenIds()
        => [.. _cards.Where(c => c.Visibility == Visibility.Collapsed).Select(c => c.Model.Id),
            .. ViewForThisMonitor(_host.State).Widgets.Where(w => _cards.All(c => !ReferenceEquals(c.Model, w))).Select(w => w.Id)];

    // -----------------------------------------------------------------------------------------
    // Moving the selection
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-1584] A move starts on <paramref name="card"/>: it moves, and so does every other selected
    /// widget on this display. A card outside the selection moves alone (pressing it
    /// selected it alone). [S-11879] On the locked desktop the selection is the desktop's one: a held card
    /// in it carries every selected icon and card of this display's view.
    /// </summary>
    public void BeginMove(WidgetCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        BeginMove(card, card.PressPoint);
    }

    /// <summary>
    /// [S-1584] <see cref="BeginMove(WidgetCard)"/> with the pointer's press at <paramref name="press"/> (the widget layer's
    /// DIPs): [S-13046] held by a desktop icon, the move shows the held icon's picture there, following the pointer ([S-13048]: it alone).
    /// </summary>
    internal void BeginMove(WidgetCard card, Point press)
    {
        ArgumentNullException.ThrowIfNull(card);

        var view = ViewForThisMonitor(_host.State);
        var ids = !EditMode
            ? _host.DeskMoving(view, card.Model.Id)
            : _host.Selection.Contains(card.Model.Id) && string.Equals(_host.Selection.ViewId, view.Id, StringComparison.Ordinal)
                ? SelectionRules.InView(view, _host.Selection.Ids)
                : [];
        if (!ids.Contains(card.Model.Id))
        {
            ids.Add(card.Model.Id);
        }

        // [S-13042] A fence moves its icons with it: they stay on its cells.
        foreach (var fence in ids.Where(id => view.Find(id)?.Config is IHoldsDesktopItems).ToList())
        {
            ids.AddRange(_host.FenceIconsOf(view, fence).Where(icon => !ids.Contains(icon)));
        }

        var cards = new List<(WidgetCard Card, FracRect Start)>();
        foreach (var id in ids)
        {
            if (CardFor(id) is { Visibility: Visibility.Visible } moving)
            {
                cards.Add((moving, moving.LiveRect));
            }
        }

        if (!cards.Exists(c => ReferenceEquals(c.Card, card)))
        {
            cards.Add((card, card.LiveRect));
        }

        // [S-13041] Icons alone land on a fence too (its cells are the grid's): the held icon lands by the one drop
        // computation (on a fence's body, on its cell). A move may land over a card that stays: nothing keeps widgets apart.
        var models = cards.ConvertAll(c => c.Card.Model);
        var cell = _host.DropCellFor(this, models);

        // [S-13046] Held by a desktop icon: the held icon under the pointer, held where it was grabbed; [S-13048] it alone,
        // however many icons move.
        HandedMove = null;
        var heldByIcon = card.View is IDesktopIconView;
        var pressAt = _widgetLayer.TransformToVisual(_root).TransformPoint(press);
        _move = new MoveSession(card, cards, PlacementPayload.FromView(view, ids, PlacementMode.Move, card.Model.Id), Block(cards, cards.FindIndex(c => ReferenceEquals(c.Card, card)), cell))
        {
            HeldByIcon = heldByIcon,
            Picture = heldByIcon ? DeskDragPicture(card, cards.Count(c => c.Card.View is IDesktopIconView), pressAt) : null,
            Press = pressAt,
            Takes = heldByIcon && EditMode ? Themia.Core.Desktop.UnplacedItems.Taken(view, cards.Select(c => c.Card.Model.Id)) : [],
        };
    }

    /// <summary>[S-11890] The two translations of a move of <paramref name="cards"/>, on this display as it is now; [S-13041] the held card's landing on the cell of what it lands on.</summary>
    private BlockMove Block(List<(WidgetCard Card, FracRect Start)> cards, int held = -1, Func<FracRect, FracRect>? cell = null)
        => new([.. cards.Select(c => c.Start)], Metrics, held, cell);

    /// <summary>
    /// [S-1584] The pointer moved: every moving card follows by the same translation ([S-11892]: whole cells, every card
    /// on the grid's lines, each line on its pixel, [S-11873]). [S-11891] They are drawn where they land: snapped to the
    /// grid as a block (<see cref="BlockGeometry.Move"/>), clamped for the block as a whole, which the local grid lights. [S-78] The display the drop would hand the widgets to shows their ghost, and the cards here fade.
    /// [S-11890] The first move is drawn at once; the ones after are coalesced to one step a rendered frame
    /// (<see cref="PointerFrames"/>, <see cref="CompositionTarget.Rendering"/>), the latest pointer winning.
    /// </summary>
    public void PreviewMove(WidgetCard card, double dxFrac, double dyFrac)
    {
        if (_move is not { } move || !ReferenceEquals(move.Anchor, card) || !Metrics.IsUsable)
        {
            return;
        }

        if (!move.Moved)
        {
            StepMove(move, dxFrac, dyFrac);
            return;
        }

        if (move.Frames.Offer(dxFrac, dyFrac))
        {
            CompositionTarget.Rendering += OnMoveFrame;
        }
    }

    /// <summary>[S-11890] A frame is about to be drawn: the move takes the pointer's latest place.</summary>
    private void OnMoveFrame(object? sender, object args)
    {
        CompositionTarget.Rendering -= OnMoveFrame;
        if (_move is { } move && !_closed && move.Frames.Take() is { } at)
        {
            StepMove(move, at.Dx, at.Dy);
        }
    }

    /// <summary>[S-11890] The move's waiting pointer step, if any, applied now (before a drop, and by the bench).</summary>
    internal void FlushMove()
    {
        CompositionTarget.Rendering -= OnMoveFrame;
        if (_move is { } move && move.Frames.Take() is { } at)
        {
            StepMove(move, at.Dx, at.Dy);
        }
    }

    /// <summary>
    /// [S-11890] One step of a move, touching only what changed: the moving cards' offsets, the local grid and the
    /// border marks ([S-11874], [S-11881]) when where the block lands changed (another cell); the grid and marks also
    /// when the hand-off preview came or went. The first step also frees the moving cards from their neighbours
    /// (one join pass, [S-2244]); after it the joins stand still until the drop: the moving cards keep their places to
    /// one another and join only among themselves ([S-2457]). Nothing is stored and no event raised until the drop.
    /// </summary>
    private void StepMove(MoveSession move, double dxFrac, double dyFrac)
    {
        var metrics = Metrics;
        if (!metrics.IsUsable)
        {
            return;
        }

        // [S-2343] [S-3710] Nothing hangs over the selection while it is dragged; its page opens where it lands.
        ClearForDrag();

        DragProfile.Start();
        var first = !move.Moved;
        var landed = move.Block.Step(dxFrac, dyFrac);
        DragProfile.Lap(DragStage.Geometry);

        if (first || landed)
        {
            foreach (var (moving, start) in move.Cards)
            {
                var at = move.Block.At(start, move.Block.Landing);
                moving.SetLiveRect(at);
                moving.FollowMove(GridGeometry.ToPixels(at, metrics, RasterScale));
            }

            // [S-2460] The selection's frame goes with the block ([S-14310]: on the locked desktop its corners).
            SyncFrame();
        }

        DragProfile.Lap(DragStage.Position);

        if (first)
        {
            // [S-2244] Dragged cards are free; the ones they leave get their corners back at once.
            move.Moved = true;
            JoinCards();

            // [S-13046] What moves is drawn over every other card (an icon over the fence it lands in), and the icons a
            // desktop icon's move carries stand where they land as their ghosts.
            LiftMoving(move);
            if (move.HeldByIcon)
            {
                foreach (var (moving, _) in move.Cards.Where(c => c.Card.View is IDesktopIconView))
                {
                    moving.SetGhost(true);
                }
            }
        }

        var leaving = move.Payload is { } payload && _host.PreviewHandOff(this, payload);

        // [S-13713] Over the open Unplaced icons page with icons it takes: let go there, they leave the view. The page is lit, and the
        // cards here fade as for a hand-off, the local grid off: they would not land on the desktop.
        var pointer = new Point(move.Press.X + (dxFrac * metrics.Width), move.Press.Y + (dyFrac * metrics.Height));
        var overPage = !leaving && move.Takes.Count > 0 && OverUnplacedPage(pointer);
        var away = leaving || overPage;
        var leavingChanged = first || away != (move.Leaving || move.OverPage);
        if (first || overPage != move.OverPage)
        {
            ShowUnplacedDrop(overPage);
        }

        move.Leaving = leaving;
        move.OverPage = overPage;
        if (leavingChanged)
        {
            foreach (var (moving, _) in move.Cards)
            {
                moving.SetLeaving(away);
            }
        }

        // [S-13046] The icon under the pointer, every frame; not while the block goes to another display, which shows its ghost.
        // [S-13713] Over the page too: the drag's picture is over every layer, the flyouts' included.
        if (move.Picture is { } picture)
        {
            if (leaving)
            {
                HideDragPicture();
            }
            else
            {
                ShowDragPicture(picture, pointer);
            }
        }

        DragProfile.Lap(DragStage.HandOff);

        if (first || leavingChanged || landed)
        {
            // [S-11874] The local grid round where the block lands (edit mode: its box lit over the whole grid), not
            // while the block is going to another display or onto the Unplaced icons page. [S-11881] On the locked desktop the
            // borders near it with it.
            var target = !away ? move.Block.LandingBounds() : (FracRect?)null;
            ShowLocalGrid(target);
            DragProfile.Lap(DragStage.LocalGrid);
            ShowMoveBorders(EditMode ? null : target, move);
            DragProfile.Lap(DragStage.Marks);
        }
    }

    /// <summary>[S-13046] Every moving card over every other card while it moves, in the order they had among themselves (a fence's icons over it).</summary>
    private void LiftMoving(MoveSession move)
    {
        var top = _cards.Count;
        foreach (var moving in move.Cards.Select(c => c.Card).OrderBy(c => Canvas.GetZIndex(c)).ToList())
        {
            Canvas.SetZIndex(moving, top++);
        }
    }

    /// <summary>[S-13046] The move is over (landed, handed off or undone): the picture goes, the icons are whole again and every card back in the view's paint order.</summary>
    private void EndMoveLook(MoveSession move)
    {
        if (move.Picture is not null)
        {
            HideDragPicture();
        }

        foreach (var (moving, _) in move.Cards)
        {
            moving.SetGhost(false);
        }

        ApplyPaintOrder(ViewForThisMonitor(_host.State));
    }

    /// <summary>
    /// [S-11881] While a block moves on the locked desktop: the moving cards' corner marks, and the marks of the
    /// cards near where it lands fading as the local grid does (<see cref="LocalGrid.Borders"/>); none with null.
    /// </summary>
    private void ShowMoveBorders(FracRect? target, MoveSession move)
    {
        var moving = move.Cards.Select(c => c.Card).ToHashSet();
        var borders = target is { } box
            ? LocalGrid.Borders(
                box,
                moving.Select(c => c.Model.Id),
                _cards.Where(c => !moving.Contains(c) && c.Visibility == Visibility.Visible && c.IsShown(EditMode)).Select(c => (c.Model.Id, c.LiveRect)),
                Metrics)
            : new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var card in _cards)
        {
            card.ShowMoveMarks(borders.TryGetValue(card.Model.Id, out var alpha) ? alpha : null);
        }
    }

    /// <summary>
    /// [S-11878] Whether the move <paramref name="card"/> holds becomes the shell's drag now: it is a desktop
    /// icon's on the locked desktop, it carries files, and the pointer is off the desktop or over a drop
    /// target (<see cref="Themia.Core.Desktop.DesktopDragOut"/>).
    /// </summary>
    public bool DragsOut(WidgetCard card)
    {
        if (_move is not { Moved: true } move || !ReferenceEquals(move.Anchor, card) || EditMode || card.View is not IDesktopIconView
            || !Interop.Win32.GetCursorPos(out var at))
        {
            return false;
        }

        var moving = move.Cards.Select(c => c.Card).ToHashSet();
        return Themia.Core.Desktop.DesktopDragOut.Starts(
            heldIsIcon: true,
            carriesFiles: _host.DeskCarriesFiles(moving.Select(c => c.Model)),
            overDesktop: _host.OverDesktop(at),
            overDropTarget: DropTargetUnder(ToContent(at.X, at.Y), moving));
    }

    /// <summary>
    /// [S-11878] A widget under <paramref name="point"/> that takes a drop of files, the moving cards looked through. [S-13041]
    /// [S-13047] Never a fence nor an icon a fence holds (<see cref="Themia.Core.Desktop.DesktopDragOut.DropTarget"/>): the
    /// desktop's own icons land on its cells by the move, the picture under the pointer the same.
    /// </summary>
    internal bool DropTargetUnder(Point point, IReadOnlySet<WidgetCard> moving)
    {
        foreach (var element in VisualTreeHelper.FindElementsInHostCoordinates(point, _root))
        {
            for (DependencyObject? at = element; at is not null; at = VisualTreeHelper.GetParent(at))
            {
                if (at is WidgetCard card)
                {
                    if (moving.Contains(card))
                    {
                        break;
                    }

                    return Themia.Core.Desktop.DesktopDragOut.DropTarget(
                        takesDrops: card.View is IDesktopIconView icon ? icon.TakesDrops : card.View is Themia.App.Widgets.IWidgetDropTarget,
                        fenced: _host.FencesOf(ViewForThisMonitor(_host.State)).FenceAt(card.Model.Id) is not null);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// [S-11878] The move became the shell's drag: every moving card back where it started, nothing stored, nothing handed off.
    /// [S-13047] Its press and its picture are kept for the drag (<see cref="HandedMove"/>).
    /// </summary>
    public void CancelMove(WidgetCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (_move is not { } move || !ReferenceEquals(move.Anchor, card))
        {
            return;
        }

        _move = null;
        HandedMove = (ToScreen(move.Press), move.Picture);
        CompositionTarget.Rendering -= OnMoveFrame;
        _host.EndHandOffPreview();
        ShowUnplacedDrop(false);
        ShowLocalGrid(null);
        ShowMoveBorders(null, move);
        foreach (var (moving, start) in move.Cards)
        {
            moving.SetLeaving(false);
            PositionCard(moving, start);
        }

        EndMoveLook(move);
        JoinCards();
    }

    /// <summary>
    /// [S-1584]/[S-1591] The move ended. Over another display showing another view, the widgets go
    /// there ([S-76]); otherwise every moved card keeps the rect it was dragged to.
    /// </summary>
    public void CommitMove(WidgetCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        // [S-11890] The pointer's last place counts: a step still waiting for its frame is taken first.
        FlushMove();
        var move = _move is { } session && ReferenceEquals(session.Anchor, card)
            ? session
            : new MoveSession(card, [(card, card.LiveRect)], null, Block([(card, card.LiveRect)]));
        _move = null;

        _host.EndHandOffPreview();
        ShowUnplacedDrop(false);
        ShowLocalGrid(null);
        ShowMoveBorders(null, move);
        foreach (var (moving, _) in move.Cards)
        {
            moving.SetLeaving(false);
        }

        EndMoveLook(move);

        // [S-13713] Let go over the open Unplaced icons page: its desktop items leave the view (one undo step) and the page lists
        // them; whatever else the move carried stays where it was.
        if (move.Moved && move.OverPage && move.Takes.Count > 0)
        {
            foreach (var (moving, start) in move.Cards)
            {
                PositionCard(moving, start);
            }

            JoinCards();
            _host.UnplaceDesktopItems(CurrentViewId, move.Takes);
            return;
        }

        if (move.Payload is { } payload && TryHandOff(payload))
        {
            return;
        }

        // [S-11891] They land where they were drawn and the local grid lit: on the grid.
        var landings = new List<Landing>(move.Cards.Count);
        foreach (var (moving, start) in move.Cards)
        {
            var rect = move.Moved ? move.Block.At(start, move.Block.Landing) : moving.LiveRect;
            landings.Add(new Landing(moving.Model.Id, rect));
            PositionCard(moving, rect);
        }

        // [S-2244] Dropped: they join where they landed, one join pass.
        JoinCards();

        // [S-884] [S-15662] The whole drag, every selected widget, is one undo step; [S-13041] its icons land in a fence or out of one in it.
        _host.Land(this, landings, UndoAction.Move);
    }

    /// <summary>
    /// [S-76]/[S-77]/[S-1591] A drag that ends with the pointer over a display showing a different
    /// view hands every dragged widget to it, keeping their identity and their offsets to the held
    /// one, whose centre lands on the pointer. The drop point, not the dragged rects, decides: those
    /// are clamped to this screen ([S-09]) and can never reach across the boundary.
    /// </summary>
    private bool TryHandOff(PlacementPayload payload)
    {
        if (_host.HandOffTarget(this, out var cursor) is not { } target)
        {
            return false;
        }

        var targetViewId = target.CurrentViewId;
        if (!_host.Land(target.PlanAt(cursor, payload), targetViewId))
        {
            return false;
        }

        Log.Info(Scope, $"{payload.Count} widget(s) handed to {target.Monitor.GdiName} (view '{targetViewId}')");
        return true;
    }

    // -----------------------------------------------------------------------------------------
    // The rubber band
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-1581] Start a rubber band at a point of empty surface. With Ctrl or Shift it adds to what is
    /// selected on this display; without, it starts from nothing.
    /// </summary>
    private void BeginLasso(PointerRoutedEventArgs e, Point at)
    {
        var additive = IsDown(Windows.System.VirtualKey.Control) || IsDown(Windows.System.VirtualKey.Shift);
        var viewId = CurrentViewId;

        List<string> kept = additive && string.Equals(_host.Selection.ViewId, viewId, StringComparison.Ordinal)
            ? [.. _host.Selection.Ids]
            : [];

        if (!additive)
        {
            _host.Select(null);
        }

        _band = Arm(e, new RubberBand(BandKind.Lasso, new BandGesture(at.X, at.Y)) { Base = kept });
    }

    /// <summary>[S-1581] The drawn lasso follows the pointer, and the selection follows the lasso live.</summary>
    private void UpdateLasso(RubberBand band, Point now)
    {
        var metrics = Metrics;
        if (!metrics.IsUsable)
        {
            return;
        }

        var rect = band.Gesture.To(now.X, now.Y);
        ShowBand(rect);

        var view = ViewForThisMonitor(_host.State);
        var hits = SelectionRules.Lasso(
            view,
            new FracRect(rect.Left / metrics.Width, rect.Top / metrics.Height, rect.Width / metrics.Width, rect.Height / metrics.Height),
            HiddenIds());
        band.Caught = hits.Count;
        _host.SelectMany(view.Id, band.Base.Concat(hits));
    }

    private void OnRootPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (EndBand(e))
        {
            e.Handled = true;
        }
    }

    private void OnRootPointerCaptureLost(object sender, PointerRoutedEventArgs e) => EndBand(null);

    /// <summary>[S-1582] Ctrl+A: select every widget the grid places on this display.</summary>
    public void SelectAll()
    {
        if (_closed)
        {
            return;
        }

        var view = ViewForThisMonitor(_host.State);
        _host.SelectMany(view.Id, SelectionRules.All(view, HiddenIds()));
    }
}
