using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Widgets;

namespace Themia.App.Shell;

/// <summary>
/// [S-2110] [S-2114] The card's frost: its display's blurred wallpaper (<see cref="Backdrop"/>, one
/// picture shared by every card there) under the fill, showing the part that lies behind the card. The
/// picture is drawn at the display's size and offset by the card's place on it, so a move, a resize or
/// a gap change shifts an offset and nothing is blurred again.
/// </summary>
internal sealed partial class WidgetCard
{
    /// <summary>The frost layer: clips nothing itself (the surface's rounded clip does), holds the picture.</summary>
    private readonly Canvas _frost = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };

    /// <summary>The display's picture, display-sized, placed at minus the card's offset.</summary>
    private readonly Image _frostPicture = new() { Stretch = Stretch.Fill, IsHitTestVisible = false };

    /// <summary>The backdrop this card watches, or null.</summary>
    private Backdrop? _backdrop;

    private long _leftWatch;
    private long _topWatch;

    /// <summary>[S-2111] Whether the blurred wallpaper lies under the card's ground: the switch is on and the display has a picture.</summary>
    private bool Frosted => _host.Design.BlurBehind && _backdrop?.Picture is not null;

    /// <summary>Follow the card's place on its surface: a move is an offset ([S-2114]).</summary>
    private void AttachBackdrop()
    {
        _frost.Children.Add(_frostPicture);
        _leftWatch = RegisterPropertyChangedCallback(Canvas.LeftProperty, (_, _) => PlaceBackdrop());
        _topWatch = RegisterPropertyChangedCallback(Canvas.TopProperty, (_, _) => PlaceBackdrop());
    }

    private void DetachBackdrop()
    {
        UnregisterPropertyChangedCallback(Canvas.LeftProperty, _leftWatch);
        UnregisterPropertyChangedCallback(Canvas.TopProperty, _topWatch);
        Watch(null);
    }

    /// <summary>[S-2118] Watch the backdrop of the display the card is on now (a display change may hand it another).</summary>
    private void BindBackdrop() => Watch(_host.Backdrop);

    private void Watch(Backdrop? backdrop)
    {
        if (ReferenceEquals(backdrop, _backdrop))
        {
            return;
        }

        if (_backdrop is not null)
        {
            _backdrop.Changed -= OnBackdropChanged;
        }

        _backdrop = backdrop;
        if (_backdrop is not null)
        {
            _backdrop.Changed += OnBackdropChanged;
        }
    }

    /// <summary>The display's picture arrived or changed: the card paints again (its fill's opacity depends on it, [S-2111]).</summary>
    private void OnBackdropChanged() => ApplyDesign();

    /// <summary>[S-2110] Draw the frost under <paramref name="fill"/> when the rule says so, else nothing.</summary>
    private void ApplyFrost(CardFill fill)
    {
        var picture = _backdrop?.Picture;
        var frosts = DesignLook.Frosts(fill, _host.Design.BlurBehind, picture is not null, HasImage);

        _frostPicture.Source = frosts ? picture : null;
        _frost.Visibility = frosts ? Visibility.Visible : Visibility.Collapsed;
        if (frosts)
        {
            PlaceBackdrop();
        }
    }

    /// <summary>
    /// [S-2114] The picture at the display's size, moved by the painted surface's offset on it (the
    /// card's place on its surface plus the gap's inset). The surface spans its display ([S-840]).
    /// </summary>
    private void PlaceBackdrop()
    {
        if (_frost.Visibility != Visibility.Visible || _surface is null)
        {
            return;
        }

        var metrics = _host.Metrics;
        var left = Canvas.GetLeft(this);
        var top = Canvas.GetTop(this);
        _frostPicture.Width = metrics.Width;
        _frostPicture.Height = metrics.Height;
        Canvas.SetLeft(_frostPicture, -((double.IsFinite(left) ? left : 0) + _surface.Margin.Left));
        Canvas.SetTop(_frostPicture, -((double.IsFinite(top) ? top : 0) + _surface.Margin.Top));
    }
}
