using Microsoft.UI.Xaml;
using Themia.Sdk.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12815] A list whose rows are the first thing in its card stands the card's inset over its view's top, so its first
/// row's band reaches the card's top edge and its text stands about the inset under it, as every list's first row does
/// (owner, 2026-11-19: "there should not be a space at the top"); its rows run to the card's foot edge
/// (<c>ItemSize.Rows</c>). The one place a list sets that margin.
/// </summary>
public static class ListEdge
{
    /// <summary>The margin of a list standing on the card's top edge, its rows running to the card's foot edge.</summary>
    public static Thickness Margin => new(0, -FamilySpace.Inset, 0, 0);

    /// <summary>Stand <paramref name="part"/> (a list's scroll, page or stack) on the card's top edge.</summary>
    public static void Apply(FrameworkElement part)
    {
        ArgumentNullException.ThrowIfNull(part);
        part.Margin = Margin;
    }
}
