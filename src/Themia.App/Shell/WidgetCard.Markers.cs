using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Shell;

/// <summary>
/// [S-31b]/[S-31c] The edit-mode markers: no outline round the card, only a square-cornered bracket
/// at each corner and a short bar at the centre of each edge. They are the eight resize handles too;
/// <see cref="EditMarkers"/> places them, this paints them. The grips (which take the presses) are the
/// card's own; the ink is on two plates the surface's <see cref="EditMarkerLayer"/> draws above every
/// card, every halo under every bar, so where two widgets meet their marks are one line. [S-1597] A
/// selected card's grips and its move area are lifted onto a third plate above every card
/// (<see cref="MarkerLift"/>), so a selected card under another is still resized and moved by all of it
/// (<see cref="SelectionRules.PressTarget"/>); the cards' drawing order stays.
/// </summary>
internal sealed partial class WidgetCard
{
    /// <summary>The eight grips, one <see cref="ResizeHandleDot"/> each, over everything the card paints.</summary>
    private readonly Canvas _markers;

    /// <summary>The halos of the eight markers, in the card's own coordinates, placed where the card stands.</summary>
    internal Canvas MarkerHalos { get; } = new() { IsHitTestVisible = false };

    /// <summary>The bars of the eight markers, in the card's own coordinates, placed where the card stands.</summary>
    internal Canvas MarkerInks { get; } = new() { IsHitTestVisible = false };

    private readonly List<Marker> _markerParts = [];

    /// <summary>
    /// [S-1597] The plate a selected card's grips stand on in edit mode, above every card: its move area under
    /// its eight grips, in the card's own coordinates, placed where the card stands. Takes the presses it gets
    /// as the card does.
    /// </summary>
    internal Canvas MarkerLift { get; } = new();

    /// <summary>[S-1597] The selected card's move area on the lift plate: the whole card, under its grips.</summary>
    private readonly ResizeHandleDot _liftBody = new() { Background = Family.Brush(ColourRole.Clear) };

    /// <summary>[S-1597] Whether the grips are on the lift plate now (selected, in edit mode, on a surface).</summary>
    private bool _lifted;

    /// <summary>The size and scale the markers were last laid out for, so a repeat is free.</summary>
    private (double Width, double Height, double Scale) _markersLaidOut = (-1, -1, -1);

    private XamlRoot? _markerRoot;

    /// <summary>The contrast stroke under every bar, opaque.</summary>
    internal static readonly Brush MarkerHalo = ShellBrush.Of(ShellRole.CardBase);

    /// <summary>The theme's neutral strong stroke, translucent: never drawn itself (see <see cref="NeutralInk"/>).</summary>
    private static readonly Brush MarkerNeutral = ShellBrush.Of(ShellRole.StrongFill);

    private static SolidColorBrush? _neutralInk;

    /// <summary>
    /// [S-31b] The neutral bars' one brush: the theme's strong stroke as it looks over the halo every
    /// bar stands on, made opaque, so where two neighbours' bars overlap they read as one bar instead
    /// of a brighter square. Follows both theme brushes as they are recoloured in place.
    /// </summary>
    internal static SolidColorBrush NeutralInk()
    {
        if (_neutralInk is null)
        {
            _neutralInk = new SolidColorBrush();
            Blend();
            MarkerNeutral.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, (_, _) => Blend());
            MarkerHalo.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, (_, _) => Blend());
        }

        return _neutralInk;

        static void Blend()
        {
            if (_neutralInk is not null && MarkerNeutral is SolidColorBrush ink && MarkerHalo is SolidColorBrush under)
            {
                // The halo's own colour (opaque), recoloured to the bar over it: derived from the two theme brushes.
                var bar = ink.Color;
                var shown = under.Color;
                (shown.R, shown.G, shown.B) = EditMarkers.Over((bar.A, bar.R, bar.G, bar.B), (shown.R, shown.G, shown.B));
                _neutralInk.Color = shown;
            }
        }
    }

    /// <summary>One handle: its grip, the contrast stroke under each bar (on the halo plate), and the bars (on the ink plate).</summary>
    private sealed record Marker(ResizeHandle Handle, ResizeHandleDot Grip, Rectangle[] Halos, Rectangle[] Inks);

    private Canvas BuildMarkers()
    {
        var canvas = new Canvas { Visibility = Visibility.Collapsed };
        var halo = MarkerHalo;

        foreach (var handle in Enum.GetValues<ResizeHandle>())
        {
            var bars = IsCorner(handle) ? 2 : 1;

            // Transparent, not null: the whole grip takes the press, not just its ink.
            var grip = new ResizeHandleDot { Background = Family.Brush(ColourRole.Clear), Tag = handle };
            grip.SetCursorShape(CursorFor(handle));

            // Every halo under every bar (two plates), so an L's two strokes merge instead of crossing its
            // joint, and a neighbour's halo never cuts this card's bar. Neither takes input: the press must
            // land on the grip, whose Tag names the handle.
            var halos = Enumerable.Range(0, bars).Select(_ => new Rectangle { Fill = halo, IsHitTestVisible = false }).ToArray();
            var inks = Enumerable.Range(0, bars).Select(_ => new Rectangle { IsHitTestVisible = false }).ToArray();
            foreach (var part in halos)
            {
                MarkerHalos.Children.Add(part);
            }

            foreach (var part in inks)
            {
                MarkerInks.Children.Add(part);
            }

            canvas.Children.Add(grip);
            _markerParts.Add(new Marker(handle, grip, halos, inks));
        }

        // [S-1597] The lift plate: the move area first, the grips over it while lifted.
        _liftBody.SetCursorShape(InputSystemCursorShape.SizeAll);
        MarkerLift.Children.Add(_liftBody);
        MarkerLift.Visibility = Visibility.Collapsed;
        MarkerLift.PointerPressed += OnPointerPressed;
        MarkerLift.RightTapped += OnRightTapped;

        SizeChanged += (_, _) => LayoutMarkers();
        Loaded += (_, _) => WatchScale(XamlRoot);
        Unloaded += (_, _) => WatchScale(null);

        // The plates stand where the card stands and show when its grips do.
        RegisterPropertyChangedCallback(Canvas.LeftProperty, (_, _) => FollowPlates());
        RegisterPropertyChangedCallback(Canvas.TopProperty, (_, _) => FollowPlates());
        RegisterPropertyChangedCallback(VisibilityProperty, (_, _) => FollowPlates());
        RegisterPropertyChangedCallback(OpacityProperty, (_, _) => FollowPlates());
        canvas.RegisterPropertyChangedCallback(VisibilityProperty, (_, _) => FollowPlates());
        FollowPlates();
        return canvas;
    }

    /// <summary>Place both plates on the card and show them exactly while its grips show.</summary>
    private void FollowPlates()
    {
        var shown = Visibility == Visibility.Visible && _markersShown ? Visibility.Visible : Visibility.Collapsed;
        foreach (var plate in new[] { MarkerHalos, MarkerInks })
        {
            Canvas.SetLeft(plate, Canvas.GetLeft(this));
            Canvas.SetTop(plate, Canvas.GetTop(this));
            plate.Opacity = Opacity * MoveMarksAlpha;
            plate.Visibility = shown;
        }

        Canvas.SetLeft(MarkerLift, Canvas.GetLeft(this));
        Canvas.SetTop(MarkerLift, Canvas.GetTop(this));
        MarkerLift.Visibility = _lifted && shown == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// [S-1597] Lift the grips onto <see cref="MarkerLift"/> (above every card) or put them back on the card. Only on
    /// a surface whose marker layer holds the plate; a card on none keeps them.
    /// </summary>
    private void Lift(bool up)
    {
        up &= MarkerLift.Parent is not null;
        if (up == _lifted)
        {
            return;
        }

        _lifted = up;
        (_markers.Parent as Panel)?.Children.Remove(_markers);
        (up ? MarkerLift.Children : Children).Add(_markers);
        FollowPlates();
    }

    /// <summary>Whether the marks show (edit mode); the field is read before the grip canvas exists.</summary>
    private bool _markersShown;

    /// <summary>[S-11881] The strength of this card's marks while a block moves on the locked desktop; null when none moves.</summary>
    private double? _moveMarks;

    /// <summary>[S-11881] The plates' strength: a locked move's for this card, else full.</summary>
    private double MoveMarksAlpha => !_host.EditMode && _moveMarks is { } alpha ? Math.Clamp(alpha, 0, 1) : 1;

    /// <summary>
    /// [S-11881] While a block moves on the locked desktop, show this card's corner marks (edit mode's look)
    /// at <paramref name="alpha"/>: 1 for a moving card, the local grid's strength for a neighbour near where
    /// it lands (<see cref="LocalGrid.Borders"/>); null takes them away again, as the block lands.
    /// </summary>
    public void ShowMoveMarks(double? alpha)
    {
        if (_moveMarks == alpha)
        {
            return;
        }

        _moveMarks = alpha;
        ApplySelection();
    }

    /// <summary>Take the plates off the surface's marker layer, as the card leaves it.</summary>
    private void DropPlates()
    {
        Lift(false);
        foreach (var plate in new[] { MarkerHalos, MarkerInks, MarkerLift })
        {
            (plate.Parent as Panel)?.Children.Remove(plate);
        }
    }

    /// <summary>A DPI change re-lays the markers on the new device pixels; the card's size in DIPs may not change.</summary>
    private void WatchScale(XamlRoot? root)
    {
        if (_markerRoot is not null)
        {
            _markerRoot.Changed -= OnMarkerRootChanged;
        }

        _markerRoot = root;
        if (root is not null)
        {
            root.Changed += OnMarkerRootChanged;
        }

        LayoutMarkers();
    }

    private void OnMarkerRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => LayoutMarkers();

    /// <summary>[S-31b] Place the grips and bars for the card's current size and scale.</summary>
    private void LayoutMarkers()
    {
        var scale = XamlRoot?.RasterizationScale ?? 1.0;
        _liftBody.Width = Math.Max(0, ActualWidth);
        _liftBody.Height = Math.Max(0, ActualHeight);
        var key = (ActualWidth, ActualHeight, scale);
        if (ActualWidth <= 0 || ActualHeight <= 0 || key == _markersLaidOut)
        {
            return;
        }

        _markersLaidOut = key;
        var layout = EditMarkers.For(ActualWidth, ActualHeight, scale).ToDictionary(m => m.Handle);
        foreach (var part in _markerParts)
        {
            var marker = layout[part.Handle];
            Canvas.SetLeft(part.Grip, marker.Hit.X);
            Canvas.SetTop(part.Grip, marker.Hit.Y);
            part.Grip.Width = marker.Hit.Width;
            part.Grip.Height = marker.Hit.Height;

            // The plates are in the card's coordinates, so each bar is offset by its grip's corner.
            for (var i = 0; i < part.Inks.Length; i++)
            {
                var bar = marker.Bars[i];
                var onCard = new PixelRect(marker.Hit.X + bar.X, marker.Hit.Y + bar.Y, bar.Width, bar.Height);
                Place(part.Inks[i], onCard);
                Place(part.Halos[i], EditMarkers.Haloed(onCard, scale));
            }
        }

        static void Place(Rectangle shape, PixelRect box)
        {
            Canvas.SetLeft(shape, box.X);
            Canvas.SetTop(shape, box.Y);
            shape.Width = box.Width;
            shape.Height = box.Height;
        }
    }

    /// <summary>
    /// [S-31c] Selected, the bars take the accent; otherwise a neutral strong stroke, so every widget in
    /// edit mode shows where it is and which ones the commands act on. Where a selected widget meets
    /// one that is not, the selected one's bars are on top.
    /// </summary>
    private void PaintMarkers(bool selected)
    {
        Canvas.SetZIndex(MarkerInks, selected ? 1 : 0);
        var ink = selected
            ? ThemeBrush.Of("AccentFillColorDefaultBrush", Colors.DodgerBlue)
            : NeutralInk();
        foreach (var part in _markerParts)
        {
            foreach (var bar in part.Inks)
            {
                bar.Fill = ink;
            }
        }
    }

    /// <summary>
    /// [S-2444] A fixed axis has no handle: the edges of that axis lose their grip and their bar, a
    /// corner resizes the free axis alone (with that axis's cursor) and, on a widget fixed both ways,
    /// keeps its bracket as a mark but takes no press.
    /// </summary>
    public void ApplyResizable()
    {
        var limits = Limits;
        var (freeW, freeH) = (limits is not { Width.Fixed: true }, limits is not { Height.Fixed: true });
        foreach (var part in _markerParts)
        {
            var (w, h) = EditMarkers.Moves(part.Handle, freeW, freeH);
            var resizes = w || h;
            part.Grip.IsHitTestVisible = resizes;
            part.Grip.SetCursorShape(CursorFor(part.Handle, w, h));

            // [S-11877] Outside edit mode a selected card shows its four corners alone.
            var inked = IsCorner(part.Handle) || (resizes && _host.EditMode) ? Visibility.Visible : Visibility.Collapsed;
            foreach (var bar in part.Inks.Concat(part.Halos))
            {
                bar.Visibility = inked;
            }
        }
    }

    private static InputSystemCursorShape CursorFor(ResizeHandle handle, bool width, bool height)
        => width && height ? CursorFor(handle)
            : width ? InputSystemCursorShape.SizeWestEast
            : height ? InputSystemCursorShape.SizeNorthSouth
            : InputSystemCursorShape.SizeAll;

    private static bool IsCorner(ResizeHandle handle)
        => handle is ResizeHandle.TopLeft or ResizeHandle.TopRight or ResizeHandle.BottomLeft or ResizeHandle.BottomRight;

    private static InputSystemCursorShape CursorFor(ResizeHandle handle) => handle switch
    {
        ResizeHandle.TopLeft or ResizeHandle.BottomRight => InputSystemCursorShape.SizeNorthwestSoutheast,
        ResizeHandle.TopRight or ResizeHandle.BottomLeft => InputSystemCursorShape.SizeNortheastSouthwest,
        ResizeHandle.Left or ResizeHandle.Right => InputSystemCursorShape.SizeWestEast,
        _ => InputSystemCursorShape.SizeNorthSouth,
    };
}
