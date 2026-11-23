using Themia.Core.Geometry;

namespace Themia.App.Widgets;

/// <summary>
/// [S-16640] A view drawn as several cards on one (owner, 2026-11-23: "optionally it should be possible to add a block gap
/// into the widget so that they are dymanic but it looks like different widgets"): the card paints its ground (the
/// frost, its fill, its border) only under <see cref="Grounds"/>, each with the card's corners, and nothing between them,
/// so every item of a device list with its block gap stands as a card of its own while the widget stays one. Null: the
/// card is one ground, as every card. Optional: a view that is one card does not implement it.
/// </summary>
public interface IWidgetGrounds
{
    /// <summary>The boxes the card's ground stands under, in the view's own DIPs; null for the whole card.</summary>
    IReadOnlyList<DipRect>? Grounds { get; }

    /// <summary>The grounds changed (items came, went or moved, the gap was turned on or off): the card paints them again.</summary>
    event Action? GroundsChanged;
}
