using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Themia.App.Shell;

/// <summary>
/// [S-2100]..[S-2106] The shared backgrounds of touching widgets on one surface: one fill, one tint
/// and one border per group, drawn under every card, along the outline <see cref="CardGroups"/>
/// works out. The members stop painting their own card ([S-2104]); their content, header, edit
/// outline and hit-testing stay theirs.
/// <para>
/// Recomputed only when the layout it was computed from changes (the cells, which cards paint, the
/// gap, the radius, the border width): <see cref="Apply"/> is called on every drag step and keeps
/// the last answer when nothing it depends on moved. The brushes are re-applied every time, which is
/// a few property sets on cached brushes.
/// </para>
/// </summary>
internal sealed class CardGroupLayer
{
    private readonly List<GroupPaths> _pool = [];

    private List<GroupCandidate> _lastCandidates = [];
    private (double Gap, double Radius, double Border, bool Enabled) _lastParams = (double.NaN, 0, 0, false);

    /// <summary>For each candidate of the last pass, whether it is a member of a drawn group.</summary>
    private bool[] _member = [];

    private readonly List<(WidgetSurface Surface, CardOutline Outline, CardOutline? Stroke)> _drawn = [];

    private readonly HashSet<WidgetCard> _members = [];

    /// <summary>Whether <paramref name="card"/> is drawn by a group after the last <see cref="Apply"/>.</summary>
    public bool Contains(WidgetCard card) => _members.Contains(card);

    /// <summary>Under the widget layer, never hit-testable: clicks belong to the cards over it.</summary>
    public Canvas Element { get; } = new() { IsHitTestVisible = false };

    /// <summary>[S-2105] What the groups paint, in the surface's DIPs: the hit region takes these instead of the members' own rects.</summary>
    public IReadOnlyList<PixelRect> Region { get; private set; } = [];

    /// <summary>
    /// Group <paramref name="cards"/> at their live rects and draw the groups. <paramref name="enabled"/>
    /// false (an intro playing, [S-2106]) leaves every card painting its own.
    /// </summary>
    public void Apply(IReadOnlyList<WidgetCard> cards, ScreenMetrics metrics, DesignState design, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(design);

        var usable = enabled && metrics.IsUsable;
        var candidates = new List<GroupCandidate>(cards.Count);
        foreach (var card in cards)
        {
            var paints = card.Visibility == Visibility.Visible && card.PaintsOwnCard;
            candidates.Add(new GroupCandidate(
                usable ? GridGeometry.ToPixels(card.LiveRect, metrics) : default,
                paints ? card.Model.Surface : null));
        }

        var gap = double.IsFinite(design.WidgetGapPx) && design.WidgetGapPx > 0 ? design.WidgetGapPx : 0;
        var radius = DesignLook.CornerRadius(design.CornerRadiusPx);
        var look = DesignLook.Border(WidgetSurface.Card, design.BorderStyle, design.BorderWidthPx, design.BorderOpacity);
        var widthPx = look.Visible ? look.Thickness : 0;
        var parameters = (gap, radius, widthPx, usable);

        if (parameters != _lastParams || !candidates.SequenceEqual(_lastCandidates))
        {
            _lastParams = parameters;
            _lastCandidates = candidates;
            Compute(candidates, metrics, design, usable, gap, radius);
        }

        _members.Clear();
        for (var i = 0; i < cards.Count; i++)
        {
            var member = i < _member.Length && _member[i];
            cards[i].SetGrouped(member);
            if (member)
            {
                _members.Add(cards[i]);
            }
        }

        Paint(design);
    }

    private void Compute(List<GroupCandidate> candidates, ScreenMetrics metrics, DesignState design, bool usable, double gap, double radius)
    {
        _member = new bool[candidates.Count];
        _drawn.Clear();

        Region = [];
        if (usable)
        {
            Group(candidates, metrics, design, gap, radius);
        }

        while (_pool.Count < _drawn.Count)
        {
            var paths = new GroupPaths();
            Element.Children.Add(paths.Base);
            Element.Children.Add(paths.Tint);
            Element.Children.Add(paths.Stroke);
            _pool.Add(paths);
        }

        for (var g = 0; g < _pool.Count; g++)
        {
            var paths = _pool[g];
            if (g >= _drawn.Count)
            {
                paths.Hide();
                continue;
            }

            var (_, outline, stroke) = _drawn[g];
            paths.Base.Data = ToGeometry(outline);
            paths.Tint.Data = ToGeometry(outline);
            paths.Stroke.Data = stroke is null ? null : ToGeometry(stroke);
        }
    }

    private void Group(List<GroupCandidate> candidates, ScreenMetrics metrics, DesignState design, double gap, double radius)
    {
        var tolerance = CardGroups.Tolerance(metrics);
        var inset = gap / 2;
        var concave = Math.Min(radius, inset);
        var region = new List<PixelRect>();

        foreach (var group in CardGroups.Find(candidates, tolerance))
        {
            var cells = group.Members.Select(i => candidates[i].Cell).ToArray();
            var surface = candidates[group.Members[0]].Surface!.Value;

            // [S-2103] Null when the gap swallows a member: the members then paint their own cards.
            if (CardGroups.Outline(cells, tolerance, inset, radius, concave) is not { } outline)
            {
                continue;
            }

            // [S-824] The border strokes inside the fill's edge, as a lone card's does: its path runs
            // half a stroke further in, its convex corners that much tighter and its concave ones looser.
            var border = DesignLook.Border(surface, design.BorderStyle, design.BorderWidthPx, design.BorderOpacity);
            var stroke = border.Visible
                ? CardGroups.Outline(cells, tolerance, inset + (border.Thickness / 2), Math.Max(0, radius - (border.Thickness / 2)), concave + (border.Thickness / 2))
                : null;

            foreach (var i in group.Members)
            {
                _member[i] = true;
            }

            _drawn.Add((surface, outline, stroke));
            region.AddRange(outline.Region);
        }

        Region = region;
    }

    /// <summary>[S-823] [S-824] The groups' fill, tint and border, from the same answers a lone card paints with.</summary>
    private void Paint(DesignState design)
    {
        for (var g = 0; g < _drawn.Count; g++)
        {
            var paths = _pool[g];
            var surface = _drawn[g].Surface;
            var opacity = DesignLook.FillOpacity(surface, design.SurfaceOpacity);
            var tint = CardPaint.Tint(design);

            paths.Base.Visibility = Visibility.Visible;
            paths.Base.Fill = CardPaint.Base(design);
            paths.Base.Opacity = opacity;

            paths.Tint.Visibility = tint is null ? Visibility.Collapsed : Visibility.Visible;
            paths.Tint.Fill = tint;
            paths.Tint.Opacity = opacity;

            var border = DesignLook.Border(surface, design.BorderStyle, design.BorderWidthPx, design.BorderOpacity);
            if (border.Visible && paths.Stroke.Data is not null)
            {
                paths.Stroke.Visibility = Visibility.Visible;
                paths.Stroke.Stroke = CardPaint.Stroke(design);
                paths.Stroke.StrokeThickness = border.Thickness;
                paths.Stroke.Opacity = border.Opacity;
                paths.Stroke.StrokeDashArray = CardPaint.Dashes(border);
                paths.Stroke.StrokeDashCap = border.RoundCaps ? PenLineCap.Round : PenLineCap.Flat;
                paths.Stroke.StrokeStartLineCap = paths.Stroke.StrokeEndLineCap = paths.Stroke.StrokeDashCap;
            }
            else
            {
                paths.Stroke.Visibility = Visibility.Collapsed;
            }
        }
    }

    /// <summary>
    /// The outline as a path: per loop, a line into each corner and its arc out (clockwise on a
    /// convex corner, counter-clockwise on a concave one). Even-odd, so a hole stays open.
    /// </summary>
    private static PathGeometry ToGeometry(CardOutline outline)
    {
        var geometry = new PathGeometry { FillRule = FillRule.EvenOdd };
        foreach (var loop in outline.Loops)
        {
            var last = loop[^1];
            var figure = new PathFigure { StartPoint = new Point(last.OutX, last.OutY), IsClosed = true, IsFilled = true };
            foreach (var corner in loop)
            {
                figure.Segments.Add(new LineSegment { Point = new Point(corner.InX, corner.InY) });
                if (corner.Radius > 0)
                {
                    figure.Segments.Add(new ArcSegment
                    {
                        Point = new Point(corner.OutX, corner.OutY),
                        Size = new Size(corner.Radius, corner.Radius),
                        SweepDirection = corner.Convex ? SweepDirection.Clockwise : SweepDirection.Counterclockwise,
                    });
                }
            }

            geometry.Figures.Add(figure);
        }

        return geometry;
    }

    /// <summary>One group's three shapes, pooled so a drag that regroups does not rebuild elements.</summary>
    private sealed class GroupPaths
    {
        public Path Base { get; } = new() { IsHitTestVisible = false };

        public Path Tint { get; } = new() { IsHitTestVisible = false };

        public Path Stroke { get; } = new() { IsHitTestVisible = false };

        public void Hide()
        {
            Base.Visibility = Tint.Visibility = Stroke.Visibility = Visibility.Collapsed;
            Base.Data = Tint.Data = Stroke.Data = null;
        }
    }
}
