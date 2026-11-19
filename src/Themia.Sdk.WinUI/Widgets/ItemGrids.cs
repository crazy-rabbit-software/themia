using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>[S-12830] Who knows the desktop grid a view stands on: its card, on its display.</summary>
public interface IGridCellHost
{
    /// <summary>The grid's drawn pitch on the card's display, in DIPs ([S-06b]).</summary>
    double CellPitch { get; }
}

/// <summary>
/// [S-12830] The grid an element's items lay on: the pitch of the card it stands in (<see cref="IGridCellHost"/>), the
/// target pitch before it stands in one, and its display's raster scale.
/// </summary>
public static class ItemGrids
{
    /// <summary>The grid <paramref name="element"/> lays its items on.</summary>
    public static ItemGrid Of(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        var scale = element.XamlRoot?.RasterizationScale ?? 1;
        for (DependencyObject? node = element; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is IGridCellHost host)
            {
                return new ItemGrid(host.CellPitch, scale);
            }
        }

        return ItemGrid.Default(scale);
    }
}
