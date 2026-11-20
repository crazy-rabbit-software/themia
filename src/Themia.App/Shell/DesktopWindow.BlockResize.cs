using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>
/// [S-2460]..[S-2465] Several selected widgets resized as one block: the selection's frame round them in edit mode
/// (<see cref="SelectionFrame"/>, on the marker layer) and its drag. Every step is Core's (<see cref="BlockResize"/>):
/// the members scaled about the opposite edge or corner and settled on the grid, each in its own sizing; the cards are
/// drawn where they land while the pointer moves, joined among themselves, and the whole resize is one undo step.
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>[S-2460] A drag of the frame: its handle, where the pointer pressed, the members with their start rects and sizing.</summary>
    private sealed class BlockResizeSession(ResizeHandle handle, Point pressed, List<(WidgetCard Card, FracRect Start)> cards, SizeLimits?[] limits, List<FracRect>? apart)
    {
        public ResizeHandle Handle { get; } = handle;

        public Point Pressed { get; } = pressed;

        public List<(WidgetCard Card, FracRect Start)> Cards { get; } = cards;

        public SizeLimits?[] Limits { get; } = limits;

        /// <summary>[S-2465] The cards that stay, with widgets kept apart; null when they may overlap.</summary>
        public List<FracRect>? Apart { get; } = apart;

        /// <summary>Where the members stand now: where they land.</summary>
        public IReadOnlyList<FracRect> Now { get; set; } = [.. cards.Select(c => c.Start)];

        /// <summary>A step has been drawn: the members land (where they stand, the start again included).</summary>
        public bool Stepped { get; set; }

        public bool Holds(WidgetCard card) => Cards.Exists(c => ReferenceEquals(c.Card, card));
    }

    private BlockResizeSession? _blockResize;

    private bool _frameHooked;

    /// <summary>[S-14310] The cards the frame was last drawn round (none without a frame): they show no marks of their own.</summary>
    private List<WidgetCard> _framed = [];

    /// <summary>
    /// [S-2460] [S-14310] The cards the selection's frame goes round on this display now, in the view's order
    /// (<see cref="SelectionRules.Framed{T}"/>): of the cards drawn here, the selected ones (edit mode: its selection's,
    /// every widget; the locked desktop: the desktop's one selection's cards, never an icon); none when fewer than two are,
    /// nor while a widget is placed.
    /// </summary>
    private List<WidgetCard> FrameMembers()
    {
        if (_closed || !Metrics.IsUsable || _host.Placing is not null)
        {
            return [];
        }

        if (_blockResize is { } session)
        {
            return session.Cards.ConvertAll(c => c.Card);
        }

        var editing = EditMode;
        var ofThisView = !editing || string.Equals(_host.Selection.ViewId, ViewForThisMonitor(_host.State).Id, StringComparison.Ordinal);
        return SelectionRules.Framed(
            editing,
            _cards.Where(c => c.Visibility == Visibility.Visible && c.IsShown(editing))
                .Select(c => (c, ofThisView && IsSelected(c), c.View is IDesktopIconView)));
    }

    /// <inheritdoc />
    public int FramedWith(WidgetCard card) => _framed.Contains(card) ? _framed.Count : 0;

    /// <summary>
    /// [S-2460] The frame round the selection as it stands now: round two or more selected cards of this display
    /// (<see cref="FrameMembers"/>), where they are drawn (a move's or a resize's step included); in edit mode its hairline,
    /// markers and grips, [S-14310] on the locked desktop its four corners alone. A card that joins or leaves the frame
    /// draws its own marks again, so a card alone selected loses them as a second is added and gets them back as one goes.
    /// </summary>
    private void SyncFrame()
    {
        if (!_frameHooked)
        {
            _frameHooked = true;
            _markerLayer.Frame.Pressed += OnFramePressed;
            _markerLayer.Frame.Dragged += OnFrameDragged;
            _markerLayer.Frame.Released += EndBlockResize;
        }

        var cards = FrameMembers();
        var before = _framed;
        _framed = cards;
        if (before.Count != cards.Count || !before.SequenceEqual(cards))
        {
            foreach (var card in before.Union(cards).Where(_cards.Contains).ToList())
            {
                card.ApplySelection();
            }
        }

        if (cards.Count == 0)
        {
            _markerLayer.Frame.Hide();
            return;
        }

        var box = GridGeometry.ToPixels(BlockGeometry.Bounds([.. cards.Select(c => c.LiveRect)]), Metrics, RasterScale);
        var editing = EditMode;
        var (width, height) = editing ? BlockResize.Free([.. cards.Select(c => c.Limits)], cards.Count) : (false, false);
        _markerLayer.Frame.Show(box, RasterScale, width, height, cornersOnly: !editing);
    }

    /// <summary>[S-2460] A grip of the frame pressed: the selection's resize as a block starts.</summary>
    private void OnFramePressed(ResizeHandle handle, PointerRoutedEventArgs e)
    {
        var cards = FrameMembers();
        if (!EditMode || _blockResize is not null || _move is not null || cards.Count < 2 || !Metrics.IsUsable)
        {
            _markerLayer.Frame.ReleaseHeld();
            return;
        }

        var apart = Design.WidgetCollisions ? Staying(cards) : null;
        _blockResize = new BlockResizeSession(handle, e.GetCurrentPoint(_root).Position, cards.ConvertAll(c => (c, c.LiveRect)), [.. cards.Select(c => c.Limits)], apart);

        // [S-2343] [S-3710] Nothing hangs over the selection while it is resized; its page opens as it lands. The keys stay the surface's.
        ClearForDrag();
        _root.Focus(FocusState.Pointer);
    }

    /// <summary>
    /// [S-2460] The pointer moved: the block is laid again from where it started and every member drawn where it lands,
    /// showing the config its size means ([S-2442]); the members join among themselves ([S-2457]).
    /// </summary>
    private void OnFrameDragged(PointerRoutedEventArgs e)
    {
        var metrics = Metrics;
        if (_blockResize is not { } session || !metrics.IsUsable)
        {
            return;
        }

        var now = e.GetCurrentPoint(_root).Position;
        var rects = BlockResize.Resize(
            [.. session.Cards.Select(c => c.Start)],
            session.Limits,
            session.Handle,
            (now.X - session.Pressed.X) / metrics.Width,
            (now.Y - session.Pressed.Y) / metrics.Height,
            metrics,
            session.Apart);
        if (rects.SequenceEqual(session.Now))
        {
            return;
        }

        session.Now = rects;
        session.Stepped = true;
        for (var i = 0; i < session.Cards.Count; i++)
        {
            var card = session.Cards[i].Card;
            card.PreviewResize(rects[i]);
            PositionCard(card, rects[i]);
        }

        JoinCards();
    }

    /// <summary>
    /// [S-2460] The frame let go: every member lands where it was drawn, with the config its size means, and the whole
    /// resize is one undo step ([S-884]); the selection's page opens ([S-2343]).
    /// </summary>
    private void EndBlockResize()
    {
        if (_blockResize is not { } session)
        {
            return;
        }

        _blockResize = null;
        var setConfig = false;
        if (session.Stepped)
        {
            for (var i = 0; i < session.Cards.Count; i++)
            {
                var card = session.Cards[i].Card;
                card.LandResize(session.Now[i]);
                setConfig |= card.ResizeSetConfig;
                PositionCard(card, card.Model.Rect);
            }
        }

        JoinCards();
        if (session.Stepped)
        {
            // [S-2442] A member whose config its size set (a list's rows) is stored with it, one step; [S-13040] a fence is laid again in it.
            if (setConfig)
            {
                _host.Mutate(_ => { }, relayout: true, UndoAction.Resize);
            }
            else
            {
                _host.LandMoved(this, [.. session.Cards.Select(c => c.Card.Model.Id)], UndoAction.Resize);
            }
        }

        PressEnded(session.Cards[0].Card);
    }
}
