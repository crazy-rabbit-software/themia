using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Widgets;
using Themia.Core.Geometry;
using Themia.Core.Shell;
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
    /// <summary>The ground: the outline filled, less the windows over selected widgets ([S-2344]).</summary>
    private readonly Microsoft.UI.Xaml.Shapes.Path _ground = new()
    {
        Fill = ShellBrush.Of(ShellRole.CardBase),
        IsHitTestVisible = true,
    };

    /// <summary>
    /// [S-2344] The ground thinned over a selected widget the page covers, so the widget is seen through
    /// it. It still takes every press: the widget seen through it never does.
    /// </summary>
    private readonly Microsoft.UI.Xaml.Shapes.Path _shine = new()
    {
        Fill = ShellBrush.Of(ShellRole.CardBase),
        Opacity = F.ShineThrough,
        IsHitTestVisible = true,
    };

    /// <summary>The one rim round the panel and its beak.</summary>
    private readonly Microsoft.UI.Xaml.Shapes.Path _rim = new()
    {
        Stroke = ShellBrush.Of(ShellRole.FlyoutStroke),
        StrokeThickness = FamilyShape.Hairline,
        IsHitTestVisible = false,
    };

    private FlyoutSpot _spot;

    private IReadOnlyList<PixelRect> _under = [];

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
        Children.Add(_ground);
        Children.Add(_shine);
        Children.Add(_rim);
        Children.Add(_scroll);
        Shadow = new ThemeShadow();
        Translation = new Vector3(0, 0, ShellMetrics.Shadow.Elevation);

        // A press on the flyout is the flyout's: never the surface's (a rubber band, a deselect).
        PointerPressed += (_, e) => e.Handled = true;

        // [S-2346] A press held on the page, the controls' own included: the surface does not draw the page again
        // under it, so the row pressed is the row released and its click lands.
        AddHandler(PointerPressedEvent, new PointerEventHandler((_, _) => Pressed?.Invoke()), handledEventsToo: true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler((_, _) => LetGo?.Invoke()), handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, new PointerEventHandler((_, _) => LetGo?.Invoke()), handledEventsToo: true);
        AddHandler(PointerCanceledEvent, new PointerEventHandler((_, _) => LetGo?.Invoke()), handledEventsToo: true);
        AddHandler(PointerMovedEvent, new PointerEventHandler((_, e) =>
        {
            var buttons = e.GetCurrentPoint(this).Properties;
            if (!buttons.IsLeftButtonPressed && !buttons.IsRightButtonPressed && !buttons.IsMiddleButtonPressed)
            {
                LetGo?.Invoke();
            }
        }), handledEventsToo: true);
        page.BringIntoView += Reveal;

        // [S-2347] The user's own scrolling takes over from a glide.
        _scroll.AddHandler(PointerWheelChangedEvent, new PointerEventHandler((_, _) => UserScrolls()), handledEventsToo: true);
        _scroll.AddHandler(PointerPressedEvent, new PointerEventHandler((_, _) => UserScrolls()), handledEventsToo: true);
        Unloaded += (_, _) =>
        {
            StopGlide();
            _pending = null;
            _scroll.LayoutUpdated -= OnSettled;
        };
    }

    /// <summary>[S-2347] The opened item waiting for the page and the flyout to settle at their new sizes.</summary>
    private FrameworkElement? _pending;

    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _glide;

    private (double From, double To, System.Diagnostics.Stopwatch Since)? _gliding;

    /// <summary>[S-2347] The item last revealed and where the page stood before: worked out again where the flyout is hung at another height.</summary>
    private (FrameworkElement Element, double From)? _revealed;

    /// <summary>[S-2347] Where the waiting reveal scrolls from: the page's offset before the first reveal of the item.</summary>
    private double? _revealFrom;

    /// <summary>[S-2347] The user scrolls: a glide stops, and nothing is revealed again for them.</summary>
    private void UserScrolls()
    {
        StopGlide();
        _revealed = null;
    }

    /// <summary>
    /// [S-2347] Scroll <paramref name="element"/> (an opened card, a picker, a deep link's row) into sight as Karpo does:
    /// once the page has grown and the flyout has been hung again at its new height (an opened card makes the flyout
    /// taller before it makes it scroll), only as far as it takes, gliding.
    /// </summary>
    private void Reveal(FrameworkElement element)
    {
        StopGlide();
        _pending = element;
        _revealFrom = null;
        _scroll.LayoutUpdated -= OnSettled;
        _scroll.LayoutUpdated += OnSettled;
    }

    /// <summary>
    /// [S-2347] A layout pass done: once the viewport is the flyout's room and the page's extent its height (both after a
    /// change of size, the flyout's own following the page's), the scroll that shows the waiting item, from where it is.
    /// </summary>
    private void OnSettled(object? sender, object e)
    {
        if (_pending is not { } element)
        {
            _scroll.LayoutUpdated -= OnSettled;
            return;
        }

        try
        {
            if (element.XamlRoot is null || !IsUnder(element))
            {
                _pending = null;
                _scroll.LayoutUpdated -= OnSettled;
                return;
            }

            var room = Height - F.BeakHeight;
            if (_scroll.ViewportHeight <= 0 || (!double.IsNaN(room) && Math.Abs(_scroll.ViewportHeight - room) > 0.5) || Math.Abs(_scroll.ExtentHeight - Page.ActualHeight) > 0.5)
            {
                return;
            }

            _pending = null;
            _scroll.LayoutUpdated -= OnSettled;
            var top = element.TransformToVisual(Page).TransformPoint(new Point(0, 0)).Y;
            var from = _revealFrom ?? _scroll.VerticalOffset;
            _revealFrom = null;
            _revealed = (element, from);
            var to = PageReveal.ScrollIntoView(from, _scroll.ViewportHeight, top, element.ActualHeight, _scroll.ExtentHeight, F.Gutter);
            from = _scroll.VerticalOffset;
            if (!PageReveal.Moves(from, to))
            {
                return;
            }

            if (!Glides)
            {
                _ = _scroll.ChangeView(null, to, null, disableAnimation: true);
                return;
            }

            _gliding = (from, to, System.Diagnostics.Stopwatch.StartNew());
            _glide ??= NewGlide();
            _glide.Start();
        }
        catch (Exception ex)
        {
            _pending = null;
            _scroll.LayoutUpdated -= OnSettled;
            Core.Diagnostics.Log.Error("pages", "an opened item could not be scrolled into sight", ex);
        }
    }

    /// <summary>[S-2347] Whether the page glides (Windows' animation effects on); a render's flyout jumps.</summary>
    public bool Glides { get; init; } = Motion.SystemMotion.AnimationsEnabled;

    private Microsoft.UI.Dispatching.DispatcherQueueTimer NewGlide()
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(PageReveal.FrameMs);
        timer.IsRepeating = true;
        timer.Tick += (_, _) =>
        {
            if (_gliding is not { } glide)
            {
                StopGlide();
                return;
            }

            var elapsed = glide.Since.Elapsed.TotalMilliseconds;
            _ = _scroll.ChangeView(null, PageReveal.Glide(glide.From, glide.To, elapsed), null, disableAnimation: true);
            if (PageReveal.Done(elapsed))
            {
                StopGlide();
            }
        };
        return timer;
    }

    private void StopGlide()
    {
        _gliding = null;
        _glide?.Stop();
    }

    private bool IsUnder(DependencyObject element)
    {
        for (var node = element; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, Page))
            {
                return true;
            }
        }

        return false;
    }

    public PageView Page { get; }

    /// <summary>[S-2346] A press landed on the flyout, a control's included.</summary>
    public event Action? Pressed;

    /// <summary>[S-2346] The press on the flyout let go: released, lost, cancelled, or the pointer seen with no button down.</summary>
    public event Action? LetGo;

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
        // [S-2347] Hung at another height after an item was revealed: the reveal worked out again from where the page stood.
        if (_revealed is { } shown && Math.Abs(Height - (spot.Height + F.BeakHeight)) > 0.5 && _pending is null)
        {
            StopGlide();
            _pending = shown.Element;
            _revealFrom = shown.From;
            _scroll.LayoutUpdated -= OnSettled;
            _scroll.LayoutUpdated += OnSettled;
        }

        Width = spot.Width;
        Height = spot.Height + F.BeakHeight;
        Canvas.SetLeft(this, spot.X);
        Canvas.SetTop(this, spot.Y - F.BeakHeight);
        _spot = spot;
        _rim.Data = Outline(spot.Width, spot.Height, spot.Tip);
        Draw();
    }

    /// <summary>
    /// [S-2344] The selected widgets as their cards are drawn now (the surface's DIPs): where the panel
    /// covers them its ground is thinned so they are seen through it. Empty for every other page.
    /// </summary>
    public void ShineThrough(IReadOnlyList<PixelRect> widgets)
    {
        _under = widgets ?? throw new ArgumentNullException(nameof(widgets));
        Draw();
    }

    /// <summary>The ground less the windows, and the windows thinly: one geometry each, so a window is thinned once.</summary>
    private void Draw()
    {
        var windows = FlyoutPlacement.ShineThrough(_spot, _under);
        var ground = new GeometryGroup { FillRule = FillRule.EvenOdd };
        ground.Children.Add(Outline(_spot.Width, _spot.Height, _spot.Tip));
        var shine = new GeometryGroup { FillRule = FillRule.Nonzero };
        foreach (var window in windows)
        {
            ground.Children.Add(Window(window, _spot.Width, _spot.Height));
            shine.Children.Add(Window(window, _spot.Width, _spot.Height));
        }

        _ground.Data = ground;
        _shine.Data = shine;
        _shine.Visibility = windows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>[S-2327] Scroll by a page or to an end (Page Up / Down, Home, End).</summary>
    public bool Scroll(Windows.System.VirtualKey key)
    {
        UserScrolls();
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

    /// <summary>
    /// [S-2344] A window in the panel's ground, on the outline's own lines where it meets the panel's
    /// edge (half a hairline in, under the beak's 8) and round where it takes a corner of the panel.
    /// </summary>
    private static PathGeometry Window(ShineWindow window, double width, double height)
    {
        var h = FamilyShape.Hairline / 2;
        var r = ShellMetrics.OverlayRadius;
        var left = Math.Max(window.Box.X, h);
        var right = Math.Min(window.Box.X + window.Box.Width, width - h);
        var top = F.BeakHeight + Math.Max(window.Box.Y, h);
        var bottom = F.BeakHeight + Math.Min(window.Box.Y + window.Box.Height, height - h);
        var (tl, tr, br, bl) = (window.TopLeft ? r : 0, window.TopRight ? r : 0, window.BottomRight ? r : 0, window.BottomLeft ? r : 0);

        var figure = new PathFigure { StartPoint = new Point(left + tl, top), IsClosed = true, IsFilled = true };
        void Line(double x, double y) => figure.Segments.Add(new LineSegment { Point = new Point(x, y) });
        void Arc(double x, double y) => figure.Segments.Add(new ArcSegment { Point = new Point(x, y), Size = new Size(r, r), SweepDirection = SweepDirection.Clockwise });

        Line(right - tr, top);
        if (tr > 0)
        {
            Arc(right, top + tr);
        }

        Line(right, bottom - br);
        if (br > 0)
        {
            Arc(right - br, bottom);
        }

        Line(left + bl, bottom);
        if (bl > 0)
        {
            Arc(left, bottom - bl);
        }

        Line(left, top + tl);
        if (tl > 0)
        {
            Arc(left + tl, top);
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }
}
