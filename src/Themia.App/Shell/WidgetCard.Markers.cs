using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.Core.Geometry;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>
/// [S-31b]/[S-31c] The edit-mode markers: no outline round the card, only a square-cornered bracket
/// at each corner and a short bar at the centre of each edge. They are the eight resize handles too;
/// <see cref="EditMarkers"/> places them, this paints them.
/// </summary>
internal sealed partial class WidgetCard
{
    /// <summary>The eight markers, one <see cref="ResizeHandleDot"/> each, over everything the card paints.</summary>
    private readonly Canvas _markers;

    private readonly List<Marker> _markerParts = [];

    /// <summary>The size and scale the markers were last laid out for, so a repeat is free.</summary>
    private (double Width, double Height, double Scale) _markersLaidOut = (-1, -1, -1);

    private XamlRoot? _markerRoot;

    /// <summary>One handle: its grip, the contrast stroke under each bar, and the bars.</summary>
    private sealed record Marker(ResizeHandle Handle, ResizeHandleDot Grip, Rectangle[] Halos, Rectangle[] Inks);

    private Canvas BuildMarkers()
    {
        var canvas = new Canvas { Visibility = Visibility.Collapsed };
        var halo = ThemeBrush.Of("SolidBackgroundFillColorBaseBrush", Colors.White);

        foreach (var handle in Enum.GetValues<ResizeHandle>())
        {
            var bars = IsCorner(handle) ? 2 : 1;

            // Transparent, not null: the whole grip takes the press, not just its ink.
            var grip = new ResizeHandleDot { Background = new SolidColorBrush(Colors.Transparent), Tag = handle };
            grip.SetCursorShape(CursorFor(handle));

            // Every halo first, then every bar, so an L's two strokes merge instead of crossing its joint.
            // Neither takes input: the press must land on the grip, whose Tag names the handle.
            var halos = Enumerable.Range(0, bars).Select(_ => new Rectangle { Fill = halo, IsHitTestVisible = false }).ToArray();
            var inks = Enumerable.Range(0, bars).Select(_ => new Rectangle { IsHitTestVisible = false }).ToArray();
            foreach (var part in halos.Concat(inks))
            {
                grip.Children.Add(part);
            }

            canvas.Children.Add(grip);
            _markerParts.Add(new Marker(handle, grip, halos, inks));
        }

        SizeChanged += (_, _) => LayoutMarkers();
        Loaded += (_, _) => WatchScale(XamlRoot);
        Unloaded += (_, _) => WatchScale(null);
        return canvas;
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

            for (var i = 0; i < part.Inks.Length; i++)
            {
                Place(part.Inks[i], marker.Bars[i]);
                Place(part.Halos[i], EditMarkers.Haloed(marker.Bars[i], scale));
            }
        }

        static void Place(Rectangle shape, PixelRect box)
        {
            shape.HorizontalAlignment = HorizontalAlignment.Left;
            shape.VerticalAlignment = VerticalAlignment.Top;
            shape.Margin = new Thickness(box.X, box.Y, 0, 0);
            shape.Width = box.Width;
            shape.Height = box.Height;
        }
    }

    /// <summary>
    /// [S-31c] Selected, the bars take the accent; otherwise a neutral strong stroke, so every widget in
    /// edit mode shows where it is and which ones the commands act on.
    /// </summary>
    private void PaintMarkers(bool selected)
    {
        var ink = selected
            ? ThemeBrush.Of("AccentFillColorDefaultBrush", Colors.DodgerBlue)
            : ThemeBrush.Of("ControlStrongFillColorDefaultBrush", Colors.Gray);
        foreach (var part in _markerParts)
        {
            foreach (var bar in part.Inks)
            {
                bar.Fill = ink;
            }
        }
    }

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
