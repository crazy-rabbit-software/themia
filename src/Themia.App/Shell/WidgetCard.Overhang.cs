using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>
/// [S-13975] A card's overhang plate: the part of its view drawn past the card's box (<see cref="IWidgetOverhang"/>: a
/// desktop icon's name in icon only), standing where the card stands on the surface's plate above every card
/// (<see cref="EditMarkerLayer.Overhangs"/>). It knows its card, so a press on what it holds is the card's
/// (<see cref="WidgetPress.Locate"/>). Its empty room takes no press: no background.
/// </summary>
internal sealed partial class CardOverhang(WidgetCard card) : Canvas
{
    /// <summary>The card whose view's part this plate holds.</summary>
    public WidgetCard Card { get; } = card;
}

/// <summary>
/// [S-13975] The overhang: a view that draws past its box (<see cref="IWidgetOverhang"/>) has that part lifted onto the
/// card's <see cref="OverhangPlate"/>, which follows the card's place, visibility and fade, and goes with it.
/// </summary>
internal sealed partial class WidgetCard
{
    private CardOverhang? _overhangPlate;

    /// <summary>[S-13975] The plate the view's overhang stands on; the surface draws it over every card.</summary>
    internal CardOverhang OverhangPlate => _overhangPlate ??= new CardOverhang(this);

    /// <summary>[S-13975] Follow the card with the plate: called once as the card is built.</summary>
    private void BuildOverhang()
    {
        RegisterPropertyChangedCallback(Canvas.LeftProperty, (_, _) => FollowOverhang());
        RegisterPropertyChangedCallback(Canvas.TopProperty, (_, _) => FollowOverhang());
        RegisterPropertyChangedCallback(VisibilityProperty, (_, _) => FollowOverhang());
        RegisterPropertyChangedCallback(OpacityProperty, (_, _) => FollowOverhang());
        HangOverhang();
    }

    /// <summary>[S-13975] The view's overhang on the plate (the view's own, whichever view runs now), or none.</summary>
    private void HangOverhang()
    {
        var plate = OverhangPlate;
        var part = (_view as IWidgetOverhang)?.Overhang;
        if (part is not null && plate.Children.Count == 1 && ReferenceEquals(plate.Children[0], part))
        {
            return;
        }

        plate.Children.Clear();
        if (part is not null)
        {
            (part.Parent as Panel)?.Children.Remove(part);
            plate.Children.Add(part);
        }

        FollowOverhang();
    }

    /// <summary>[S-13975] The plate where the card stands, shown and faded with it.</summary>
    private void FollowOverhang()
    {
        if (_overhangPlate is not { } plate)
        {
            return;
        }

        Canvas.SetLeft(plate, Canvas.GetLeft(this));
        Canvas.SetTop(plate, Canvas.GetTop(this));
        plate.Opacity = Opacity;
        plate.Visibility = Visibility;
    }

    /// <summary>[S-13975] Take the plate off the surface, as the card leaves it.</summary>
    private void DropOverhang()
    {
        if (_overhangPlate is { } plate)
        {
            (plate.Parent as Panel)?.Children.Remove(plate);
        }
    }
}
