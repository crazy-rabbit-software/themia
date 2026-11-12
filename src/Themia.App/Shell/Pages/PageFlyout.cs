using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Widgets;
using Themia.Core.Geometry;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Windows.Foundation;
using F = Themia.Sdk.Kit.ShellMetrics.Flyout;

namespace Themia.App.Shell;

/// <summary>
/// [S-2325] [S-2326] A page's flyout hung from its entry on the edit bar, Karpo's (karpo-app, docs/widgets.md
/// §575, §584; <c>FlyoutHost</c>, <c>Beak</c>): 580 wide, the page in it scrolling past 70 % of the work
/// area; a beak 8 tall and 16 across at its top pointing at the entry, the beak and the panel one shape
/// with one rim and one shadow; radius 8; Windows' ground (never the widgets' look). No title: the
/// entry names the page.
/// </summary>
internal sealed partial class PageFlyout : Grid
{
    private readonly Microsoft.UI.Xaml.Shapes.Path _outline = new()
    {
        Fill = ShellBrush.Of(ShellRole.CardBase),
        Stroke = ShellBrush.Of(ShellRole.FlyoutStroke),
        StrokeThickness = FamilyShape.Hairline,
        IsHitTestVisible = true,
    };

    private readonly ScrollViewer _scroll = new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        VerticalScrollMode = ScrollMode.Enabled,
        IsTabStop = false,
    };

    public PageFlyout(PageView page)
    {
        Page = page ?? throw new ArgumentNullException(nameof(page));
        Width = F.Width;
        _scroll.Content = page;
        _scroll.Margin = new Thickness(0, F.BeakHeight, 0, 0);
        _scroll.CornerRadius = new CornerRadius(ShellMetrics.OverlayRadius);
        Children.Add(_outline);
        Children.Add(_scroll);
        Shadow = new ThemeShadow();
        Translation = new Vector3(0, 0, ShellMetrics.Shadow.Elevation);

        // A press on the flyout is the flyout's: never the surface's (a rubber band, a deselect).
        PointerPressed += (_, e) => e.Handled = true;
        page.BringIntoView += element => element.StartBringIntoView(new BringIntoViewOptions
        {
            AnimationDesired = false,
            VerticalAlignmentRatio = 0,

            // The gutter over it kept in view, as the page's top keeps it.
            TargetRect = new Rect(0, -F.Gutter, element.ActualWidth, element.ActualHeight + F.Gutter),
        });
    }

    public PageView Page { get; }

    /// <summary>How tall the page wants to be at the flyout's width, before the room cuts it.</summary>
    public double ContentHeight
    {
        get
        {
            Page.Measure(new Size(F.Width, double.PositiveInfinity));
            return Page.DesiredSize.Height;
        }
    }

    /// <summary>[S-2325] Stand at <paramref name="spot"/> (its top the panel's; the beak above it).</summary>
    public void Place(FlyoutSpot spot)
    {
        Width = spot.Width;
        Height = spot.Height + F.BeakHeight;
        Canvas.SetLeft(this, spot.X);
        Canvas.SetTop(this, spot.Y - F.BeakHeight);
        _outline.Data = Outline(spot.Width, spot.Height, spot.Tip);
    }

    /// <summary>[S-2327] Scroll by a page or to an end (Page Up / Down, Home, End).</summary>
    public bool Scroll(Windows.System.VirtualKey key)
    {
        var to = key switch
        {
            Windows.System.VirtualKey.PageDown => _scroll.VerticalOffset + _scroll.ViewportHeight,
            Windows.System.VirtualKey.PageUp => _scroll.VerticalOffset - _scroll.ViewportHeight,
            Windows.System.VirtualKey.Home => 0,
            Windows.System.VirtualKey.End => _scroll.ScrollableHeight,
            _ => double.NaN,
        };

        return !double.IsNaN(to) && _scroll.ChangeView(null, Math.Clamp(to, 0, _scroll.ScrollableHeight), null, disableAnimation: true);
    }

    /// <summary>
    /// [S-2326] The panel and its beak as one outline: the rounded rectangle <paramref name="width"/> ×
    /// <paramref name="height"/> under the beak's 8, the beak's tip <paramref name="tip"/> along its top,
    /// its sides at 45°; drawn half a hairline in so the rim falls on whole pixels.
    /// </summary>
    private static PathGeometry Outline(double width, double height, double tip)
    {
        var h = FamilyShape.Hairline / 2;
        var r = ShellMetrics.OverlayRadius;
        var top = F.BeakHeight + h;
        var bottom = F.BeakHeight + height - h;
        var (left, right) = (h, width - h);
        var half = F.BeakWidth / 2;

        var figure = new PathFigure { StartPoint = new Point(left + r, top), IsClosed = true, IsFilled = true };
        void Line(double x, double y) => figure.Segments.Add(new LineSegment { Point = new Point(x, y) });
        void Arc(double x, double y) => figure.Segments.Add(new ArcSegment { Point = new Point(x, y), Size = new Size(r, r), SweepDirection = SweepDirection.Clockwise });

        Line(tip - half, top);
        Line(tip, h);
        Line(tip + half, top);
        Line(right - r, top);
        Arc(right, top + r);
        Line(right, bottom - r);
        Arc(right - r, bottom);
        Line(left + r, bottom);
        Arc(left, bottom - r);
        Line(left, top + r);
        Arc(left + r, top);

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }
}
