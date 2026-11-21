using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>
/// [S-2123] Liquid glass (<see cref="CardMaterial.LiquidGlass"/>): the card drawn as <see cref="LiquidGlass"/> says,
/// every layer a plain WinUI one (no effect graph, nothing reflected: Native AOT safe). Bottom up: the lift (faint
/// rings under the card, never past a side a joined card covers so it never falls on it), the frost ([S-2110]), the tint (the fill:
/// the glass's own over a Default background, as clear as the text allows over what lies behind the whole glass
/// shape), the glow and the lens bands (under the content), the card's content, the dividers to the mates below and
/// beside it, and the rim (the card's border path). Cards that touch are one glass shape (<see cref="CardJoins.Bodies"/>):
/// every gradient runs down the body's height, not the card's, so the rim, the glow and the lens light the touching cards as
/// one piece.
/// </summary>
internal sealed partial class WidgetCard
{
    /// <summary>[S-2123] The glow (its background), over the tint and under the content.</summary>
    private readonly Border _glass = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };

    /// <summary>[S-2123] The lens: one border per band, from the rim in, over the glow and under the content.</summary>
    private readonly Grid _lens = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };

    /// <summary>[S-2123] The hairlines to the joined cards this card draws toward, over the content.</summary>
    private readonly Microsoft.UI.Xaml.Shapes.Path _dividers = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };

    /// <summary>[S-2123] The lift: its rings under the surface, standing on the surface's box.</summary>
    private readonly Grid _glassLift = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };

    /// <summary>[S-2123] The glass shape this card is part of, as the last join gave it; null before any join.</summary>
    private GlassPart? _part;

    /// <summary>[S-2123] Whether the cards are glass now.</summary>
    private bool Glass => _host.Design.Material == CardMaterial.LiquidGlass;

    /// <summary>[S-2132] [S-2133] The theme's glass as the design's tokens shape it, its tint taking the accent's share.</summary>
    private GlassLook GlassNow(bool dark) => LiquidGlass.For(dark, _host.Design.Tokens ?? new LookTokens(), DesignApplier.Accent);

    /// <summary>
    /// [S-2123] The glass shape this card is part of (<see cref="CardJoins.Bodies"/>): the glass is drawn again
    /// when it changes, so cards joined, parted or moved light as one piece.
    /// </summary>
    private void ApplyGlassPart(GlassPart part)
    {
        if (_part is { } before && before.Body == part.Body && before.Dividers.SequenceEqual(part.Dividers))
        {
            return;
        }

        _part = part;
        if (Glass)
        {
            ApplyDesign();
        }
    }

    /// <summary>
    /// [S-2123] The body's box from the painted surface's corner, in the content's own DIPs (the content is drawn at
    /// the content scale, [S-850]): where the glass's gradients start and how far they run.
    /// </summary>
    private (double X, double Y, double Width, double Height) BodyInContent()
    {
        var k = _contentScale > 0 ? 1 / _contentScale : 1;
        var surface = (Width: Math.Max(1, _surface.ActualWidth), Height: Math.Max(1, _surface.ActualHeight));
        if (_part is not { } part || part.Body.Width <= 0 || part.Body.Height <= 0)
        {
            return (0, 0, surface.Width * k, surface.Height * k);
        }

        var painted = Cell;
        return ((part.Body.X - painted.X) * k, (part.Body.Y - painted.Y) * k, part.Body.Width * k, part.Body.Height * k);
    }

    /// <summary>
    /// [S-2123] Paint the glass's layers over what <see cref="ApplyDesign"/> painted, or take them away for a solid
    /// card. <paramref name="radiusPx"/> is the card's corner radius in DIPs, <paramref name="radius"/> the corners of a
    /// layer inside the surface's clip ([S-2250]).
    /// </summary>
    private void ApplyGlass(CornerRadius radius, double radiusPx, CardFill fill)
    {
        // [S-2124] No card, no glass: a transparent widget or a desktop icon stays bare.
        if (!Glass || !LiquidGlass.Wears(CardFillNow()))
        {
            _glass.Visibility = Visibility.Collapsed;
            _lens.Visibility = Visibility.Collapsed;
            _dividers.Visibility = Visibility.Collapsed;
            _glassLift.Visibility = Visibility.Collapsed;
            return;
        }

        var dark = DesignApplier.IsDark;
        var look = GlassNow(dark);
        TintGlass(fill);

        var body = BodyInContent();

        // The glow: the light falling in at the body's top edge, GlowDepth deep; nothing over the rest.
        _glass.Visibility = Visibility.Visible;
        _glass.CornerRadius = radius;
        _glass.Background = Gradient(look.Glow, LiquidGlass.Highlight, body.Y, body.Y + (look.GlowDepth / Math.Max(0.01, _contentScale)));

        DrawLens(look, radiusPx, body);
        DrawDividers(look);

        // The rim: the card's border path, its light running down the body.
        if (!_hidden || _host.EditMode)
        {
            var k = _contentScale > 0 ? _contentScale : 1;
            _stroke.Visibility = Visibility.Visible;
            _stroke.Stroke = Gradient(look.Rim, LiquidGlass.Highlight, body.Y * k, (body.Y + body.Height) * k);
            _stroke.StrokeThickness = look.RimWidth;
            _stroke.Opacity = 1;
            _stroke.StrokeDashArray = [];
            _strokeLook = (look.RimWidth, radiusPx);
            DrawStroke();
        }

        Lift(look, radiusPx);
    }

    /// <summary>
    /// [S-2123] The lens: a band of light inside the rim, as stepped borders from the rim in (each band its weight of
    /// the lens profile down the body), on the sides the card draws: none along a side a joined card covers, where the
    /// glass runs on.
    /// </summary>
    private void DrawLens(GlassLook look, double radiusPx, (double X, double Y, double Width, double Height) body)
    {
        _lens.Visibility = Visibility.Visible;
        while (_lens.Children.Count < look.LensBands.Count)
        {
            _lens.Children.Add(new Border { IsHitTestVisible = false });
        }

        while (_lens.Children.Count > look.LensBands.Count)
        {
            _lens.Children.RemoveAt(_lens.Children.Count - 1);
        }

        var o = FlowOutline;
        var w = look.LensBandWidth / Math.Max(0.01, _contentScale);
        for (var band = 0; band < look.LensBands.Count; band++)
        {
            var at = band * w;
            var border = (Border)_lens.Children[band];
            border.BorderThickness = new Thickness(o.DrawsLeft ? w : 0, o.DrawsTop ? w : 0, o.DrawsRight ? w : 0, o.DrawsBottom ? w : 0);
            border.Margin = new Thickness(o.DrawsLeft ? at : 0, o.DrawsTop ? at : 0, o.DrawsRight ? at : 0, o.DrawsBottom ? at : 0);
            border.CornerRadius = Corners(Math.Max(0, (radiusPx / Math.Max(0.01, _contentScale)) - at));

            // The band's own box starts below the body's top by its margin.
            var top = body.Y - border.Margin.Top;
            border.BorderBrush = Gradient(LiquidGlass.LensStops(look, band), LiquidGlass.Highlight, top, top + body.Height);
        }
    }

    /// <summary>[S-2123] The hairlines toward the joined cards below and beside this card, inside its own edge.</summary>
    private void DrawDividers(GlassLook look)
    {
        if (_part is not { Dividers.Count: > 0 } part)
        {
            _dividers.Visibility = Visibility.Collapsed;
            return;
        }

        var t = Fluent.Hairline;
        var geometry = new PathGeometry();

        // The lines are physical; the path flows with the card, so a right-to-left card mirrors them back.
        var mirror = FlowDirection == FlowDirection.RightToLeft;
        double X(double x) => mirror ? Cell.Width - x : x;
        foreach (var line in part.Dividers)
        {
            // Half the hairline in from the card's edge, so the mate drawn over the edge never covers it.
            var horizontal = Math.Abs(line.Y1 - line.Y2) < 1e-9;
            var (dx, dy) = horizontal ? (0.0, -t / 2) : (-t / 2, 0.0);
            var figure = new PathFigure { StartPoint = new Point(X(line.X1 + dx), line.Y1 + dy), IsClosed = false, IsFilled = false };
            figure.Segments.Add(new LineSegment { Point = new Point(X(line.X2 + dx), line.Y2 + dy) });
            geometry.Figures.Add(figure);
        }

        _dividers.Data = geometry;
        _dividers.StrokeThickness = t;
        _dividers.Stroke = new SolidColorBrush(HexBrush.Colour(look.Divider, look.DividerAlpha));
        _dividers.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// [S-2123] The tint: over a Default background the glass's own, as opaque as the theme's text needs over the
    /// blurred wallpaper under the whole glass shape (<see cref="LiquidGlass.TintOpacity"/>), so touching cards are
    /// one tint; a colour of the user's stays theirs. Again as the card moves: what lies behind it changed.
    /// </summary>
    private void TintGlass(CardFill fill)
    {
        if (!Glass || fill.Kind != FillKind.Default)
        {
            return;
        }

        // [S-2134] Held to the text at the design's own levels: vibrant text asks more of the glass.
        var dark = DesignApplier.IsDark;
        var look = GlassNow(dark);
        var tokens = _host.Design.Tokens ?? new LookTokens();
        _fill.Background = DesignApplier.UserBrush(HexColor.From(look.Tint.R, look.Tint.G, look.Tint.B));
        _fill.Opacity = LiquidGlass.TintOpacity(look, dark, Frosted ? LightBehind() : null,
            LookRules.Label(ItemTextTier.Primary, dark, tokens), LookRules.Label(ItemTextTier.Secondary, dark, tokens));

    }

    /// <summary>[S-2123] The mean light of the blurred wallpaper under the glass shape (the body, else the painted surface); null where it is not known yet.</summary>
    private double? LightBehind()
    {
        var metrics = _host.Metrics;
        if (_backdrop is null || metrics.Width <= 0 || metrics.Height <= 0 || _surface is null)
        {
            return null;
        }

        if (_part is { Body: { Width: > 0, Height: > 0 } body })
        {
            return _backdrop.LightUnder(body.X / metrics.Width, body.Y / metrics.Height, (body.X + body.Width) / metrics.Width, (body.Y + body.Height) / metrics.Height);
        }

        var left = Canvas.GetLeft(this);
        var top = Canvas.GetTop(this);
        if (!double.IsFinite(left) || !double.IsFinite(top))
        {
            return null;
        }

        var x = left + _surface.Margin.Left;
        var y = top + _surface.Margin.Top;
        var width = Math.Max(1, Width - _surface.Margin.Left - _surface.Margin.Right);
        var height = Math.Max(1, Height - _surface.Margin.Top - _surface.Margin.Bottom);
        return _backdrop.LightUnder(x / metrics.Width, y / metrics.Height, (x + width) / metrics.Width, (y + height) / metrics.Height);
    }

    /// <summary>
    /// A vertical gradient of <paramref name="light"/> at the stops' alphas, from <paramref name="from"/> to
    /// <paramref name="to"/> down the element it paints (its own DIPs), so neighbours painting one body share it.
    /// </summary>
    private static LinearGradientBrush Gradient(IReadOnlyList<GlassStop> stops, RgbColor light, double from, double to)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, from),
            EndPoint = new Point(0, Math.Max(from + 1, to)),
            MappingMode = BrushMappingMode.Absolute,
        };
        foreach (var stop in stops)
        {
            brush.GradientStops.Add(new GradientStop
            {
                Offset = stop.Offset,
                Color = HexBrush.Colour(light, stop.Alpha),
            });
        }

        return brush;
    }

    /// <summary>
    /// [S-2123] The lift: a large, faint shadow as rings round the surface (<see cref="LiquidGlass.LiftRings"/>), under
    /// it and moved down by the shadow's offset. Never past a side the card leaves to a mate or a joined neighbour, so
    /// one card's lift never darkens the glass beside it: touching cards lift as one piece. Shown only while the frost covers
    /// the surface: the rings run under it, and over no picture the clear glass would show them.
    /// </summary>
    private void Lift(GlassLook look, double radiusPx)
    {
        if (!Frosted)
        {
            _glassLift.Visibility = Visibility.Collapsed;
            return;
        }

        _glassLift.Visibility = Visibility.Visible;
        var rings = LiquidGlass.LiftRings(look);
        while (_glassLift.Children.Count < rings.Count)
        {
            _glassLift.Children.Add(new Border { IsHitTestVisible = false });
        }

        while (_glassLift.Children.Count > rings.Count)
        {
            _glassLift.Children.RemoveAt(_glassLift.Children.Count - 1);
        }

        // In the card's flow, as the surface's corners are.
        var o = FlowOutline;
        var drop = look.ShadowOffsetY;
        for (var i = 0; i < rings.Count; i++)
        {
            var (spread, alpha) = rings[i];
            var ring = (Border)_glassLift.Children[i];
            ring.Margin = new Thickness(
                o.DrawsLeft ? -spread : 0,
                o.DrawsTop ? -Math.Max(0, spread - drop) : 0,
                o.DrawsRight ? -spread : 0,
                o.DrawsBottom ? -(spread + drop) : 0);
            ring.CornerRadius = Corners(radiusPx + spread);
            ring.Background = new SolidColorBrush(HexBrush.Colour(LiquidGlass.Shade, alpha));
        }
    }

    /// <summary>[S-2123] The lift stands on the surface's box (the gap's inset), its rings reaching out from it.</summary>
    private void PlaceLift() => _glassLift.Margin = _surface.Margin;
}
