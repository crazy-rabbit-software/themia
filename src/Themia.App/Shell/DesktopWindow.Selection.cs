using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
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
    /// <summary>DIPs a rubber band must span before it is a band rather than a click on empty surface.</summary>
    private const double LassoThreshold = 3;

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

        /// <summary>[S-11890] [S-11891] Where the block is drawn and lands: on the grid ([S-11959]: kept apart with it).</summary>
        public BlockMove Block { get; } = block;

        /// <summary>[S-11890] The pointer's moves since the last frame, the latest kept.</summary>
        public PointerFrames Frames { get; } = new();

        /// <summary>[S-78] Whether the last step showed the block going to another display.</summary>
        public bool Leaving { get; set; }
    }

    private MoveSession? _move;

    private Rectangle? _lassoBand;
    private Point _lassoStart;
    private bool _lassoing;

    /// <summary>[S-13018] Whether the edit-mode lasso is drawn (past its dead zone), and how many widgets it touches now.</summary>
    private bool _lassoDrawn;
    private int _lassoCaught;
    private List<string> _lassoBase = [];

    /// <summary>
    /// [S-11877] Whether the widget <paramref name="widgetId"/> is drawn on this surface now (a card that hides itself, [S-873],
    /// is not; [S-13042] nor an icon its fence does not show).
    /// </summary>
    internal bool Draws(string widgetId) => CardFor(widgetId) is not { } card || (card.Visibility == Visibility.Visible && card.IsShown(EditMode));

    /// <summary>[S-11877] The desktop's one selection changed: every card on the locked desktop draws whether it is in it.</summary>
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
            ids.AddRange(Services.AppHost.FenceIconsOf(view, fence).Where(icon => !ids.Contains(icon)));
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

        // [S-11959] Kept apart in edit mode and on the locked desktop alike: whatever moves (a selection, whole
        // icons among cards) never lands over a card that stays.
        // [S-13041] Icons alone land on a fence too (its cells are the grid's): its card and its icons are no obstacle then,
        // and the held icon lands by the one drop computation (on a fence's body, on its cell).
        var models = cards.ConvertAll(c => c.Card.Model);
        var cell = _host.DropCellFor(this, models);
        var apart = Design.WidgetCollisions ? Staying(cards.Select(c => c.Card), skipFences: cell is not null) : null;
        _move = new MoveSession(card, cards, PlacementPayload.FromView(view, ids, PlacementMode.Move, card.Model.Id), Block(cards, apart, cards.FindIndex(c => ReferenceEquals(c.Card, card)), cell));
    }

    /// <summary>[S-11890] The two translations of a move of <paramref name="cards"/>, on this display as it is now; [S-13041] the held card's landing on the cell of what it lands on.</summary>
    private BlockMove Block(List<(WidgetCard Card, FracRect Start)> cards, List<FracRect>? apart, int held = -1, Func<FracRect, FracRect>? cell = null)
        => new([.. cards.Select(c => c.Start)], apart, Metrics, RasterScale, held, cell);

    /// <summary>
    /// [S-11959] The rects of this display's drawn cards other than <paramref name="moving"/> (widgets and desktop
    /// icons alike; a card that hides itself, [S-873], is not drawn): what a move keeps apart from. [S-13041] With
    /// <paramref name="skipFences"/> (icons alone moving) a fence and its icons are not: icons land in a fence.
    /// </summary>
    private List<FracRect> Staying(IEnumerable<WidgetCard> moving, bool skipFences = false)
    {
        var set = moving.ToHashSet();
        var fenced = skipFences ? Services.AppHost.FencesAndTheirIcons(ViewForThisMonitor(_host.State)) : [];
        return [.. _cards.Where(c => c.Visibility == Visibility.Visible && c.IsShown(EditMode) && !set.Contains(c) && !fenced.Contains(c.Model.Id)).Select(c => c.LiveRect)];
    }

    /// <summary>
    /// [S-1584] The pointer moved: every moving card follows by the same translation ([S-11873]: in whole device
    /// pixels, so the cards keep their spacing to the pixel). [S-11891] They are drawn where they land: snapped to the
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
        }

        DragProfile.Lap(DragStage.Position);

        if (first)
        {
            // [S-2244] Dragged cards are free; the ones they leave get their corners back at once.
            move.Moved = true;
            JoinCards();
        }

        var leaving = move.Payload is { } payload && _host.PreviewHandOff(this, payload);
        var leavingChanged = first || leaving != move.Leaving;
        move.Leaving = leaving;
        if (leavingChanged)
        {
            foreach (var (moving, _) in move.Cards)
            {
                moving.SetLeaving(leaving);
            }
        }

        DragProfile.Lap(DragStage.HandOff);

        if (first || leavingChanged || landed)
        {
            // [S-11874] The local grid round where the block lands (edit mode: its box lit over the whole grid), not
            // while the block is going to another display. [S-11881] On the locked desktop the borders near it with it.
            var target = !leaving ? move.Block.LandingBounds() : (FracRect?)null;
            ShowLocalGrid(target);
            DragProfile.Lap(DragStage.LocalGrid);
            ShowMoveBorders(EditMode ? null : target, move);
            DragProfile.Lap(DragStage.Marks);
        }
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

    /// <summary>[S-11878] A widget under <paramref name="point"/> that takes a drop of files, the moving cards looked through.</summary>
    private bool DropTargetUnder(Point point, IReadOnlySet<WidgetCard> moving)
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

                    // [S-13041] A fence is no drop target for the desktop's own icons: they land on its cells by the move.
                    return card.View is IDesktopIconView icon ? icon.TakesDrops
                        : card.Model.Config is not IHoldsDesktopItems && card.View is Themia.App.Widgets.IWidgetDropTarget;
                }
            }
        }

        return false;
    }

    /// <summary>[S-11878] The move became the shell's drag: every moving card back where it started, nothing stored, nothing handed off.</summary>
    public void CancelMove(WidgetCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (_move is not { } move || !ReferenceEquals(move.Anchor, card))
        {
            return;
        }

        _move = null;
        CompositionTarget.Rendering -= OnMoveFrame;
        _host.EndHandOffPreview();
        ShowLocalGrid(null);
        ShowMoveBorders(null, move);
        foreach (var (moving, start) in move.Cards)
        {
            moving.SetLeaving(false);
            PositionCard(moving, start);
        }

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
            : new MoveSession(card, [(card, card.LiveRect)], null, Block([(card, card.LiveRect)], null));
        _move = null;

        _host.EndHandOffPreview();
        ShowLocalGrid(null);
        ShowMoveBorders(null, move);
        foreach (var (moving, _) in move.Cards)
        {
            moving.SetLeaving(false);
        }

        if (move.Payload is { } payload && TryHandOff(payload))
        {
            return;
        }

        // [S-11891] They land where they were drawn and the local grid lit: on the grid, kept apart.
        foreach (var (moving, start) in move.Cards)
        {
            moving.Model.Rect = move.Moved ? move.Block.At(start, move.Block.Landing) : moving.LiveRect;
        }

        foreach (var (moving, _) in move.Cards)
        {
            PositionCard(moving, moving.Model.Rect);
        }

        // [S-2244] Dropped: they join where they landed, one join pass.
        JoinCards();

        // [S-884] The whole drag, every selected widget, is one undo step; [S-13041] its icons land in a fence or out of one in it.
        _host.LandMoved(this, [.. move.Cards.Select(c => c.Card.Model.Id)], UndoAction.Move);
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

        _lassoBase = additive && string.Equals(_host.Selection.ViewId, viewId, StringComparison.Ordinal)
            ? [.. _host.Selection.Ids]
            : [];

        if (!additive)
        {
            _host.Select(null);
        }

        _lassoStart = at;
        _lassoDrawn = false;
        _lassoCaught = 0;
        _lassoing = _root.CapturePointer(e.Pointer);
    }

    /// <summary>[S-1581] The band follows the pointer, and the selection follows the band live.</summary>
    private void UpdateLasso(Point now)
    {
        if (!_lassoing)
        {
            return;
        }

        var metrics = Metrics;
        var left = Math.Min(_lassoStart.X, now.X);
        var top = Math.Min(_lassoStart.Y, now.Y);
        var width = Math.Abs(now.X - _lassoStart.X);
        var height = Math.Abs(now.Y - _lassoStart.Y);

        if (!metrics.IsUsable || (width < LassoThreshold && height < LassoThreshold))
        {
            return;
        }

        var band = EnsureLassoBand();
        Microsoft.UI.Xaml.Controls.Canvas.SetLeft(band, left);
        Microsoft.UI.Xaml.Controls.Canvas.SetTop(band, top);
        band.Width = width;
        band.Height = height;
        band.Visibility = Visibility.Visible;

        var view = ViewForThisMonitor(_host.State);
        var hits = SelectionRules.Lasso(
            view,
            new FracRect(left / metrics.Width, top / metrics.Height, width / metrics.Width, height / metrics.Height),
            HiddenIds());
        _lassoDrawn = true;
        _lassoCaught = hits.Count;
        _host.SelectMany(view.Id, _lassoBase.Concat(hits));
    }

    private void OnRootPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (EndDesktopBand(e) || EndFenceBand(e))
        {
            e.Handled = true;
            return;
        }

        if (_lassoing)
        {
            _root.ReleasePointerCapture(e.Pointer);
            var (drawn, caught) = (_lassoDrawn, _lassoCaught);
            EndLasso();
            e.Handled = true;

            // [S-13018] A lasso let go over empty desktop offers its area's menu.
            if (drawn && _host.LassoReleased(this, _lassoStart, e.GetCurrentPoint(_root).Position, caught, ReleaseHeldBand))
            {
                HoldBand(_lassoBand);
            }
        }
    }

    private void OnRootPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        EndDesktopBand(null);
        EndFenceBand(null);
        EndLasso();
    }

    /// <summary>Take the band down. What it selected stays selected.</summary>
    private void EndLasso()
    {
        _lassoing = false;
        _lassoDrawn = false;
        _lassoBase = [];
        HideBand(_lassoBand);
    }

    private Rectangle EnsureLassoBand()
    {
        if (_lassoBand is not null)
        {
            return _lassoBand;
        }

        // The ghost's look, solid rather than dashed: an accent outline over a faint accent wash.
        _lassoBand = new Rectangle
        {
            Stroke = Themia.Sdk.WinUI.Kit.Family.Brush(ColourRole.Accent),
            StrokeThickness = FamilyShape.Hairline,
            Fill = HexBrush.Wash(ColourRole.Accent),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };
        _ghostLayer.Children.Add(_lassoBand);
        return _lassoBand;
    }

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
