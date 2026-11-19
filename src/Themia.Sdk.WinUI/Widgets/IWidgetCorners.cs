using Microsoft.UI.Xaml;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11883] A view whose own fill reaches the card's edge (a quick toggle's or a power button's tile,
/// whose whole face is the button) takes the card's corners for that fill: round where the card is
/// round, square where a neighbour joins it ([S-2241]). Without it the fill kept its own round
/// corners inside a joined panel, a notch at every seam (owner, 2026-11-15: "the button widgets when in
/// a group have rounded corners which looks off").
/// <para>
/// The card tells it, in the view's own DIPs (the card's corners divided by its content scale), when
/// the view is attached and whenever the corners change: a join, the design's radius, the content
/// scale. Optional: a view whose fills stand inside the card's inset does not implement it.
/// </para>
/// </summary>
public interface IWidgetCorners
{
    void ApplyCorners(CornerRadius corners);
}
