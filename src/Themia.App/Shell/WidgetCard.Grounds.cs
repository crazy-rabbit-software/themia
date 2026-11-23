using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Widgets;
using Themia.Core.Geometry;
using Themia.Core.Widgets;

namespace Themia.App.Shell;

/// <summary>
/// [S-16640] A view drawn as several cards on one (<see cref="IWidgetGrounds"/>: a device list with its block gap; owner,
/// 2026-11-23: "so that they are dymanic but it looks like different widgets"): the card's own ground (its frost, fill,
/// background image, glass, grain and border) steps back and each of the view's grounds is painted in its place, with the
/// card's corners, its fill and border and the frost of what lies behind it, so nothing is painted between them. The card
/// stays one widget: one box to select, move and resize, its gaps its own to press. Redrawn with the design and whenever
/// the view says its grounds changed; a card whose view has none is painted as before.
/// </summary>
internal sealed partial class WidgetCard
{
    /// <summary>The grounds, in the content's DIPs (the view's), under the view and over the card's own (stepped back) layers.</summary>
    private readonly Canvas _grounds = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };

    /// <summary>The view whose grounds the card follows, or null.</summary>
    private IWidgetGrounds? _groundsOf;

    /// <summary>Whether the last design pass painted the view's grounds in place of the card's.</summary>
    private bool _groundsOn;

    /// <summary>Follow the attached view's grounds (a view swapped in may have them, the old one may have had them).</summary>
    private void WatchGrounds()
    {
        var view = _view as IWidgetGrounds;
        if (ReferenceEquals(view, _groundsOf))
        {
            return;
        }

        if (_groundsOf is not null)
        {
            _groundsOf.GroundsChanged -= OnGroundsChanged;
        }

        _groundsOf = view;
        if (view is not null)
        {
            view.GroundsChanged += OnGroundsChanged;
        }

        if (_surface is not null)
        {
            ApplyDesign();
        }
    }

    private void DropGrounds()
    {
        if (_groundsOf is not null)
        {
            _groundsOf.GroundsChanged -= OnGroundsChanged;
            _groundsOf = null;
        }
    }

    /// <summary>The view's grounds changed: the design is painted again, ending with them.</summary>
    private void OnGroundsChanged()
    {
        // A view tells its grounds while it attaches, before the card's surface is built: its first design paints them.
        if (_surface is not null)
        {
            ApplyDesign();
        }
    }

    /// <summary>
    /// [S-16640] The last step of a design pass: with grounds, the card's own ground steps back and each ground is painted
    /// as the card was (its fill and its opacity, the frost under it, its border at its weight, the card's radius); without,
    /// nothing is touched.
    /// </summary>
    private void ApplyGrounds(double radius)
    {
        WatchGroundsQuietly();
        var boxes = _groundsOf?.Grounds;
        _grounds.Children.Clear();
        _groundsOn = boxes is not null;
        if (boxes is null)
        {
            _grounds.Visibility = Visibility.Collapsed;
            return;
        }

        var s = _contentScale > 0 ? _contentScale : 1;
        var fill = _fill.Background;
        var fillOpacity = _fill.Opacity;
        var frosted = _frost.Visibility == Visibility.Visible ? _frostPicture.Source : null;
        var stroked = _stroke.Visibility == Visibility.Visible;
        var strokeBrush = _stroke.Stroke;
        var strokeWidth = _stroke.StrokeThickness / s;

        // The card's own ground steps back: what it paints is the grounds' now.
        foreach (var layer in new FrameworkElement[] { _image, _glass, _lens, _grain })
        {
            layer.Visibility = Visibility.Collapsed;
        }

        _fill.Opacity = 0;
        _frost.Visibility = Visibility.Collapsed;
        _stroke.Visibility = Visibility.Collapsed;

        // The view stands in the content at its margin (the card's top inset): its DIPs are the content's moved by it.
        var at = _view.Margin;
        foreach (var given in boxes)
        {
            var box = given with { X = given.X + at.Left, Y = given.Y + at.Top };
            var body = new Grid();
            if (frosted is not null)
            {
                var picture = new Image { Source = frosted, Stretch = Stretch.Fill, IsHitTestVisible = false };
                body.Children.Add(new Canvas { Children = { picture } });
            }

            body.Children.Add(new Border { Background = fill, Opacity = fillOpacity });
            var ground = new Border
            {
                Width = box.Width,
                Height = box.Height,
                CornerRadius = new CornerRadius(radius / s),
                BorderBrush = stroked ? strokeBrush : null,
                BorderThickness = new Thickness(stroked ? strokeWidth : 0),
                BackgroundSizing = BackgroundSizing.OuterBorderEdge,
                Child = body,
                Tag = box,
            };
            Canvas.SetLeft(ground, box.X);
            Canvas.SetTop(ground, box.Y);
            _grounds.Children.Add(ground);
        }

        _grounds.Visibility = !_hidden || _host.EditMode ? Visibility.Visible : Visibility.Collapsed;
        PlaceGroundFrost();
    }

    /// <summary>The view may have been swapped since it was watched (an error card, a set-up prompt): follow it, no repaint.</summary>
    private void WatchGroundsQuietly()
    {
        var view = _view as IWidgetGrounds;
        if (ReferenceEquals(view, _groundsOf))
        {
            return;
        }

        DropGrounds();
        _groundsOf = view;
        if (view is not null)
        {
            view.GroundsChanged += OnGroundsChanged;
        }
    }

    /// <summary>
    /// [S-2114] Each ground's frost: the display's picture at the display's size, moved by the ground's place on it (the
    /// card's place, the surface's inset and the ground's box in the content, which the content scale draws larger).
    /// </summary>
    private void PlaceGroundFrost()
    {
        if (!_groundsOn || _surface is null)
        {
            return;
        }

        var s = _contentScale > 0 ? _contentScale : 1;
        var metrics = _host.Metrics;
        var left = Canvas.GetLeft(this);
        var top = Canvas.GetTop(this);
        var cardLeft = (double.IsFinite(left) ? left : 0) + _surface.Margin.Left;
        var cardTop = (double.IsFinite(top) ? top : 0) + _surface.Margin.Top;
        foreach (var child in _grounds.Children)
        {
            if (child is not Border { Tag: DipRect box } ground
                || ground.Child is not Grid { Children: [Canvas { Children: [Image picture] }, ..] })
            {
                continue;
            }

            // Inside the ground's border, which insets its child by its weight.
            var edge = ground.BorderThickness.Left;
            picture.Width = metrics.Width / s;
            picture.Height = metrics.Height / s;
            Canvas.SetLeft(picture, -((cardLeft / s) + box.X + edge));
            Canvas.SetTop(picture, -((cardTop / s) + box.Y + edge));
        }
    }
}
