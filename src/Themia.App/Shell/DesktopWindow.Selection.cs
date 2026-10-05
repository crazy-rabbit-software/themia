using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>
/// [S-1580]..[S-1586], [S-1591] Multi-select on this display: the rubber band, Ctrl+A, and dragging
/// the whole selection as one block — clamped and snapped together, and handed to another display
/// together.
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>DIPs a rubber band must span before it is a band rather than a click on empty surface.</summary>
    private const double LassoThreshold = 3;

    /// <summary>A move of one or more cards that started on this surface.</summary>
    private sealed class MoveSession(WidgetCard anchor, List<(WidgetCard Card, FracRect Start)> cards, PlacementPayload? payload)
    {
        /// <summary>The card the pointer holds.</summary>
        public WidgetCard Anchor { get; } = anchor;

        /// <summary>Every card that moves, with the rect it started at.</summary>
        public List<(WidgetCard Card, FracRect Start)> Cards { get; } = cards;

        /// <summary>[S-1591] The same widgets as a move anchored on the held card, for a hand-off to another display.</summary>
        public PlacementPayload? Payload { get; } = payload;
    }

    private MoveSession? _move;

    private Rectangle? _lassoBand;
    private Point _lassoStart;
    private bool _lassoing;
    private List<string> _lassoBase = [];

    /// <summary>The ids of the cards on this surface that are collapsed, so draw nothing.</summary>
    private HashSet<string> HiddenIds()
        => [.. _cards.Where(c => c.Visibility == Visibility.Collapsed).Select(c => c.Model.Id)];

    // -----------------------------------------------------------------------------------------
    // Moving the selection
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-1584] A move starts on <paramref name="card"/>: it moves, and so does every other selected
    /// widget on this display. A card outside the selection moves alone (pressing it
    /// selected it alone).
    /// </summary>
    public void BeginMove(WidgetCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        var view = ViewForThisMonitor(_host.State);
        var ids = _host.Selection.Contains(card.Model.Id) && string.Equals(_host.Selection.ViewId, view.Id, StringComparison.Ordinal)
            ? SelectionRules.InView(view, _host.Selection.Ids)
            : [];
        if (!ids.Contains(card.Model.Id))
        {
            ids.Add(card.Model.Id);
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

        _move = new MoveSession(card, cards, PlacementPayload.FromView(view, ids, PlacementMode.Move, card.Model.Id));
    }

    /// <summary>
    /// [S-1584] The pointer moved: every moving card follows by the same translation, which
    /// <see cref="BlockGeometry"/> snaps and clamps for the block as a whole. [S-78] The display the
    /// drop would hand the widgets to shows their ghost, and the cards here fade.
    /// </summary>
    public void PreviewMove(WidgetCard card, double dxFrac, double dyFrac, bool snap)
    {
        if (_move is not { } move || !ReferenceEquals(move.Anchor, card))
        {
            return;
        }

        var metrics = Metrics;
        if (!metrics.IsUsable)
        {
            return;
        }

        var delta = BlockGeometry.Move([.. move.Cards.Select(c => c.Start)], dxFrac, dyFrac, metrics, snap);
        foreach (var (moving, start) in move.Cards)
        {
            PositionCard(moving, BlockGeometry.Apply(start, delta, metrics));
        }

        var leaving = move.Payload is { } payload && _host.PreviewHandOff(this, payload);
        foreach (var (moving, _) in move.Cards)
        {
            moving.SetLeaving(leaving);
        }
    }

    /// <summary>
    /// [S-1584]/[S-1591] The move ended. Over another display showing another view, the widgets go
    /// there ([S-76]); otherwise every moved card keeps the rect it was dragged to.
    /// </summary>
    public void CommitMove(WidgetCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        var move = _move is { } session && ReferenceEquals(session.Anchor, card)
            ? session
            : new MoveSession(card, [(card, card.LiveRect)], null);
        _move = null;

        _host.EndHandOffPreview();
        foreach (var (moving, _) in move.Cards)
        {
            moving.SetLeaving(false);
        }

        if (move.Payload is { } payload && TryHandOff(payload))
        {
            return;
        }

        foreach (var (moving, _) in move.Cards)
        {
            moving.Model.Rect = moving.LiveRect;
        }

        foreach (var (moving, _) in move.Cards)
        {
            PositionCard(moving, moving.LiveRect);
        }

        // [S-884] The whole drag, every selected widget, is one undo step.
        _host.CommitGeometry(UndoAction.Move);
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
        _host.SelectMany(view.Id, _lassoBase.Concat(hits));
    }

    private void OnRootPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_lassoing)
        {
            _root.ReleasePointerCapture(e.Pointer);
            EndLasso();
            e.Handled = true;
        }
    }

    private void OnRootPointerCaptureLost(object sender, PointerRoutedEventArgs e) => EndLasso();

    /// <summary>Take the band down. What it selected stays selected.</summary>
    private void EndLasso()
    {
        _lassoing = false;
        _lassoBase = [];
        if (_lassoBand is not null)
        {
            _lassoBand.Visibility = Visibility.Collapsed;
        }
    }

    private Rectangle EnsureLassoBand()
    {
        if (_lassoBand is not null)
        {
            return _lassoBand;
        }

        // The ghost's look, solid rather than dashed: an accent outline over a faint accent wash.
        var accent = Brush("AccentFillColorDefaultBrush", Colors.DodgerBlue);
        var colour = (accent as SolidColorBrush)?.Color ?? Colors.DodgerBlue;
        _lassoBand = new Rectangle
        {
            Stroke = accent,
            StrokeThickness = 1,
            Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0x26, colour.R, colour.G, colour.B)),
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
