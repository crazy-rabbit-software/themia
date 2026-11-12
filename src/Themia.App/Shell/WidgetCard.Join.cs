using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>
/// [S-2240]..[S-2244] Cards that touch become one surface: the card takes its outline from
/// <see cref="CardJoins"/> (which corners stay round, which border sides draw) and gives it to
/// everything that follows its shape: the surface's clip (the frost, the fill, a background image, the
/// header, the view and any overlay inside it), the header's own fill and the border.
/// </summary>
internal sealed partial class WidgetCard
{
    /// <summary>The outline in physical sides; free until the surface joins the cards.</summary>
    private CardOutline _outline = CardOutline.Free;

    /// <summary>The border's thickness and the card's radius the stroke was last drawn for.</summary>
    private (double Thickness, double Radius) _strokeLook;

    /// <summary>[S-2241] The card's outline, physical: what the window's hit region traces ([S-2245]).</summary>
    public CardOutline Outline => _outline;

    /// <summary>[S-2244] A resize of this card is under way: it is free until dropped.</summary>
    public bool IsResizing => _dragging && _activeHandle is not null;

    /// <summary>
    /// [S-2240] Whether the card draws a card surface to join: a background that is not Transparent,
    /// or a picture of its own. A bare widget (a desktop item, a sticker) joins nothing.
    /// </summary>
    public bool DrawsSurface => CardFillNow().Kind != FillKind.Transparent || !string.IsNullOrEmpty(Model.BackgroundImage);

    /// <summary>The outline in the card's own flow: right to left the start is the physical right.</summary>
    private CardOutline FlowOutline => FlowDirection == FlowDirection.RightToLeft ? _outline.Mirrored() : _outline;

    /// <summary>
    /// [S-2240] [S-2244] The one path that joins the cards of a surface, the desktop's and a scene's:
    /// each card's painted box (its live rect's cell inside the gap), none for a card that draws no
    /// surface, is hidden, or is <paramref name="free"/> (dragged or resized), then the rule.
    /// </summary>
    public static void Join(IReadOnlyList<WidgetCard> cards, ScreenMetrics metrics, double gapPx, double rasterScale, bool editMode, Func<WidgetCard, bool> free)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(free);
        if (!metrics.IsUsable)
        {
            return;
        }

        var boxes = new PixelRect?[cards.Count];
        for (var i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            var joins = card.Visibility == Visibility.Visible && card.IsShown(editMode) && card.DrawsSurface && !free(card);
            boxes[i] = joins ? GridGeometry.Inset(GridGeometry.ToPixels(card.LiveRect, metrics), gapPx) : null;
        }

        var outlines = CardJoins.Outlines(boxes, CardJoins.Tolerance(rasterScale));
        for (var i = 0; i < cards.Count; i++)
        {
            cards[i].ApplyOutline(outlines[i]);
        }
    }

    /// <summary>[S-2241] Take a new outline: the corners and the border follow at once; nothing else is redone.</summary>
    private void ApplyOutline(CardOutline outline)
    {
        if (outline == _outline)
        {
            return;
        }

        _outline = outline;
        ApplyDesign();
    }

    /// <summary>[S-2241] The card's corner radius on its round corners, square on the others, in the card's flow.</summary>
    private CornerRadius Corners(double radius)
    {
        var o = FlowOutline;
        return new CornerRadius(
            o.RoundTopLeft ? radius : 0,
            o.RoundTopRight ? radius : 0,
            o.RoundBottomRight ? radius : 0,
            o.RoundBottomLeft ? radius : 0);
    }

    /// <summary>
    /// [S-407] [S-2241] The header's own fill's corners: the strip's outer corners, each only where the
    /// card's is round. A side strip is turned with its top facing out, so its top corners are the
    /// card's two on that edge: counter-clockwise (the left edge) its top left is the card's bottom
    /// left, clockwise (the right edge) its top left is the card's top right.
    /// </summary>
    private CornerRadius HeaderCorners(double radius)
    {
        var o = FlowOutline;
        var corners = HeaderStrip.StripCorners(ShownEdge);
        if (HeaderStrip.IsSide(Model.ShownHeader))
        {
            var left = HeaderStrip.Turn(Model.ShownHeader) == HeaderTurn.CounterClockwise;
            var physical = _outline;
            var (topLeft, topRight) = left
                ? (physical.RoundBottomLeft, physical.RoundTopLeft)
                : (physical.RoundTopRight, physical.RoundBottomRight);
            return new CornerRadius(topLeft ? radius : 0, topRight ? radius : 0, 0, 0);
        }

        return new CornerRadius(
            corners.TopStart && o.RoundTopLeft ? radius : 0,
            corners.TopEnd && o.RoundTopRight ? radius : 0,
            corners.BottomEnd && o.RoundBottomRight ? radius : 0,
            corners.BottomStart && o.RoundBottomLeft ? radius : 0);
    }

    /// <summary>
    /// [S-2243] The border's path for the surface's size: one closed rounded figure for a free card,
    /// broken where a side is left to the neighbour. Redrawn when the size, the stroke or the outline
    /// changes; content insets never depend on it.
    /// </summary>
    private void DrawStroke()
    {
        var width = _surface.ActualWidth;
        var height = _surface.ActualHeight;
        if (_stroke.Visibility != Visibility.Visible || width <= 0 || height <= 0)
        {
            return;
        }

        var geometry = new PathGeometry();
        foreach (var figure in FlowOutline.Stroke(width, height, _strokeLook.Thickness, _strokeLook.Radius))
        {
            var path = new PathFigure { StartPoint = new Point(figure.StartX, figure.StartY), IsClosed = figure.Closed, IsFilled = false };
            foreach (var segment in figure.Segments)
            {
                var to = new Point(segment.X, segment.Y);
                path.Segments.Add(segment.ArcRadius > 0
                    ? new ArcSegment { Point = to, Size = new Size(segment.ArcRadius, segment.ArcRadius), SweepDirection = SweepDirection.Clockwise }
                    : new LineSegment { Point = to });
            }

            geometry.Figures.Add(path);
        }

        _stroke.Data = geometry;
    }
}
