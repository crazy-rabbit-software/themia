using Microsoft.UI.Xaml;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-13975] A view that draws a part of itself past its card's box, over its neighbours and above every card: a desktop
/// icon's name in icon only while it is pointed at, focused or renamed, as Windows draws an icon's expanded label
/// ([S-13974]). A card clips its content to its box, so the card lifts <see cref="Overhang"/> onto its surface's overhang
/// plate, drawn over every card, standing where the card stands (the card's top-left is the part's origin) and shown and
/// faded with the card. A press on the part is the card's (a rename box in it keeps the card's keyboard lease). On a
/// surface with no overhang plate (a picture of one card) the part is not shown; what it says must be said elsewhere
/// too (a tip, a screen reader's name).
/// </summary>
public interface IWidgetOverhang
{
    /// <summary>
    /// The part, laid out by the view in the view's own DIPs from the card's top-left corner. Its own room takes no press;
    /// only what it holds that is hit-testable does.
    /// </summary>
    FrameworkElement Overhang { get; }
}
