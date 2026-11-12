using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-663] A view that measures its fixed parts on the box it is given: a graph's length, where a text
/// wraps, a grid's cells, a scroll that ends on a whole line. It never chooses what to show by the box:
/// what a widget shows is its config, the same at every size of its range ([S-2445], [S-11800]).
/// <para>
/// The card tells it, rather than every view wiring its own <c>SizeChanged</c> and each reading a
/// slightly different box. The area is the view's own slot in the card: the card less its header, edge
/// to edge, before any inset the view applies itself.
/// </para>
/// <para>
/// Called once per layout pass that changed the area, after arrange — never for a size that has
/// not been measured, and never twice for the same size. The slot is a star row of a fixed-size
/// card, so nothing the view lays out in response can change the area again: there is no loop to
/// guard against. Optional: a view that lays out by its own measure does not implement it.
/// </para>
/// </summary>
public interface IWidgetSizeAware
{
    void ApplySize(WidgetArea area);
}
