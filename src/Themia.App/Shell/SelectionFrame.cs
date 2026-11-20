using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using Themia.Core.Geometry;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Shell;

/// <summary>
/// [S-2460] The frame round a selection of several widgets (owner, 2026-11-20: "when multiple ones are
/// selected and you basically grab the edge of the selection this should be done"): one accent hairline round the
/// block's box and its eight resize grips, a single card's look (<see cref="EditMarkers"/>: a bracket at each corner, a
/// bar at the centre of each edge, each over its halo). Its halos and bars stand on the marker layer's plates with every
/// card's, so where the frame meets a member's marks they are one line; its grips stand over every card's lifted grips
/// (<see cref="EditMarkerLayer.Lifts"/>), so a press on the frame's edge or corner is the block's even where a member's
/// own grip lies under it. An axis no member may resize has no grip (<see cref="BlockResize.Free"/>), as a single card's
/// fixed axis has none. It draws and reports presses; the surface does the resize (<see cref="BlockResize"/>).
/// [S-14310] On the locked desktop it is the block's four corners alone, a locked selected card's look round the selected
/// cards of several (no hairline, no edge bars, no grips), and the members show no marks of their own in either mode.
/// </summary>
internal sealed partial class SelectionFrame
{
    private readonly List<Part> _parts = [];
    private readonly Rectangle _outline = new() { IsHitTestVisible = false };
    private uint? _pointer;

    public SelectionFrame()
    {
        var ink = WidgetCard.SelectedInk();
        _outline.Stroke = ink;
        Inks.Children.Add(_outline);
        foreach (var handle in Enum.GetValues<ResizeHandle>())
        {
            var bars = WidgetCard.IsCorner(handle) ? 2 : 1;
            var grip = new FrameGrip { Background = Family.Brush(ColourRole.Clear), Tag = handle };
            grip.SetCursorShape(WidgetCard.CursorFor(handle, true, true));
            var halos = Enumerable.Range(0, bars).Select(_ => new Rectangle { Fill = WidgetCard.MarkerHalo, IsHitTestVisible = false }).ToArray();
            var inks = Enumerable.Range(0, bars).Select(_ => new Rectangle { Fill = ink, IsHitTestVisible = false }).ToArray();
            foreach (var halo in halos)
            {
                Halos.Children.Add(halo);
            }

            foreach (var bar in inks)
            {
                Inks.Children.Add(bar);
            }

            grip.PointerPressed += OnGripPressed;
            Grips.Children.Add(grip);
            _parts.Add(new Part(handle, grip, halos, inks));
        }

        Grips.PointerMoved += OnPointerMoved;
        Grips.PointerReleased += OnPointerReleased;
        Grips.PointerCaptureLost += OnPointerCaptureLost;
        Hide();
    }

    /// <summary>The halos under the frame's bars: on the marker layer's halo plate.</summary>
    public Canvas Halos { get; } = UiLanguage.KeepLeftToRight(new Canvas { IsHitTestVisible = false });

    /// <summary>The frame's hairline and bars: on the marker layer's ink plate.</summary>
    public Canvas Inks { get; } = UiLanguage.KeepLeftToRight(new Canvas { IsHitTestVisible = false });

    /// <summary>The frame's eight grips: over every card's lifted grips. Its empty room takes no press.</summary>
    public Canvas Grips { get; } = UiLanguage.KeepLeftToRight(new Canvas());

    /// <summary>Whether the frame is drawn now.</summary>
    public bool Shown { get; private set; }

    /// <summary>[S-2460] A grip was pressed with the left button and holds the pointer: the block's resize by that handle starts.</summary>
    public event Action<ResizeHandle, PointerRoutedEventArgs>? Pressed;

    /// <summary>[S-2460] The pointer a grip holds moved.</summary>
    public event Action<PointerRoutedEventArgs>? Dragged;

    /// <summary>[S-2460] The pointer a grip held let go, or was taken from it: the resize lands.</summary>
    public event Action? Released;

    /// <summary>
    /// [S-2460] Draw the frame round <paramref name="box"/> (the surface's DIPs, on device pixels: the members' cells'
    /// own edges) at <paramref name="scale"/>, its grips on the axes the block may resize. [S-14310] With
    /// <paramref name="cornersOnly"/> (the locked desktop) its four corners alone: no hairline, no edge bars, no grip.
    /// </summary>
    public void Show(PixelRect box, double scale, bool freeWidth, bool freeHeight, bool cornersOnly)
    {
        foreach (var plate in new[] { Halos, Inks, Grips })
        {
            Canvas.SetLeft(plate, box.X);
            Canvas.SetTop(plate, box.Y);
            plate.Visibility = Visibility.Visible;
        }

        Grips.Visibility = cornersOnly ? Visibility.Collapsed : Visibility.Visible;
        if (cornersOnly)
        {
            (freeWidth, freeHeight) = (false, false);
        }

        // The hairline a device pixel wide, just outside the box, so it never covers a member's own pixels.
        var s = double.IsFinite(scale) && scale > 0 ? scale : 1;
        var line = FamilyShape.Hairline / s;
        Canvas.SetLeft(_outline, -line);
        Canvas.SetTop(_outline, -line);
        _outline.Width = box.Width + (2 * line);
        _outline.Height = box.Height + (2 * line);
        _outline.StrokeThickness = line;
        _outline.Visibility = cornersOnly ? Visibility.Collapsed : Visibility.Visible;

        var layout = EditMarkers.For(box.Width, box.Height, s).ToDictionary(m => m.Handle);
        foreach (var part in _parts)
        {
            var marker = layout[part.Handle];
            Canvas.SetLeft(part.Grip, marker.Hit.X);
            Canvas.SetTop(part.Grip, marker.Hit.Y);
            part.Grip.Width = marker.Hit.Width;
            part.Grip.Height = marker.Hit.Height;

            var (w, h) = EditMarkers.Moves(part.Handle, freeWidth, freeHeight);
            var resizes = w || h;
            part.Grip.IsHitTestVisible = resizes;
            part.Grip.SetCursorShape(WidgetCard.CursorFor(part.Handle, w, h));

            // A corner keeps its bracket as the frame's mark; an edge whose axis does not resize has none.
            var inked = WidgetCard.IsCorner(part.Handle) || resizes ? Visibility.Visible : Visibility.Collapsed;
            for (var i = 0; i < part.Inks.Length; i++)
            {
                var bar = marker.Bars[i];
                var onBox = new PixelRect(marker.Hit.X + bar.X, marker.Hit.Y + bar.Y, bar.Width, bar.Height);
                Place(part.Inks[i], onBox);
                Place(part.Halos[i], EditMarkers.Haloed(onBox, s));
                part.Inks[i].Visibility = inked;
                part.Halos[i].Visibility = inked;
            }
        }

        Shown = true;
    }

    /// <summary>Take the frame away (fewer than two framed, [S-14310]; a placement under way).</summary>
    public void Hide()
    {
        foreach (var plate in new[] { Halos, Inks, Grips })
        {
            plate.Visibility = Visibility.Collapsed;
        }

        Shown = false;
    }

    /// <summary>Let go of the pointer a grip holds, if any (the surface ends the resize itself).</summary>
    public void ReleaseHeld()
    {
        if (_pointer is not null)
        {
            _pointer = null;
            Grips.ReleasePointerCaptures();
        }
    }

    private void OnGripPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ResizeHandle handle } || _pointer is not null
            || !e.GetCurrentPoint(Grips).Properties.IsLeftButtonPressed || !Grips.CapturePointer(e.Pointer))
        {
            return;
        }

        _pointer = e.Pointer.PointerId;
        e.Handled = true;
        Pressed?.Invoke(handle, e);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_pointer == e.Pointer.PointerId)
        {
            e.Handled = true;
            Dragged?.Invoke(e);
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_pointer != e.Pointer.PointerId)
        {
            return;
        }

        e.Handled = true;
        _pointer = null;
        Grips.ReleasePointerCapture(e.Pointer);
        Released?.Invoke();
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        // Capture can be lost without a release (a display change, another window grabbing input): the resize lands
        // where it was drawn, as a single card's does.
        if (_pointer == e.Pointer.PointerId)
        {
            _pointer = null;
            Released?.Invoke();
        }
    }

    private static void Place(Rectangle shape, PixelRect box)
    {
        Canvas.SetLeft(shape, box.X);
        Canvas.SetTop(shape, box.Y);
        shape.Width = box.Width;
        shape.Height = box.Height;
    }

    /// <summary>One handle of the frame: its grip, the halos under its bars and its bars.</summary>
    private sealed record Part(ResizeHandle Handle, FrameGrip Grip, Rectangle[] Halos, Rectangle[] Inks);

    /// <summary>A frame grip: a subclass only because <c>UIElement.ProtectedCursor</c> is protected (as a card's grip).</summary>
    private sealed partial class FrameGrip : Grid
    {
        public void SetCursorShape(InputSystemCursorShape shape) => ProtectedCursor = InputSystemCursor.Create(shape);
    }
}
