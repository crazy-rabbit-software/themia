using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Widgets;

namespace Themia.App.Shell;

/// <summary>The header strip's geometry ([S-404]..[S-407]) turned into WinUI values; the rules are <see cref="HeaderStrip"/>'s.</summary>
internal sealed partial class WidgetCard
{
    /// <summary>[S-406] The edge the shown header stands on, in the card's flow: a physical left is the end right to left.</summary>
    private HeaderEdge ShownEdge => HeaderStrip.Edge(Model.ShownHeader, FlowDirection == FlowDirection.RightToLeft);

    private static void Place(FrameworkElement element, GridCell cell)
    {
        Grid.SetRow(element, cell.Row);
        Grid.SetColumn(element, cell.Column);
        Grid.SetRowSpan(element, cell.RowSpan);
        Grid.SetColumnSpan(element, cell.ColumnSpan);
    }

    private static CornerRadius Round(CardCorners corners, double radius) => new(
        corners.TopStart ? radius : 0,
        corners.TopEnd ? radius : 0,
        corners.BottomEnd ? radius : 0,
        corners.BottomStart ? radius : 0);

    private static Thickness Inset(CardSides sides, double by) => new(
        sides.Start ? by : 0,
        sides.Top ? by : 0,
        sides.End ? by : 0,
        sides.Bottom ? by : 0);
}
