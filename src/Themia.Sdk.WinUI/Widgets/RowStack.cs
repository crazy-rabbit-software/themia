using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12807] [S-12834] A stack of the family's rows (<see cref="RowBlock"/>s) laid by <see cref="SpreadStack"/>: a facts
/// list's, a forecast's days, screen time's apps, a stock's holding. [S-12813] The status glyph over the card's top end
/// corner (a stale reading's warning) stands beside the first row's value, never over it: the first row down the stack
/// ends that much sooner (<see cref="IStatusRoom"/>), the room asked of the view's status when the stack stands in it.
/// </summary>
public partial class RowStack : SpreadStack, IStatusRoom
{
    private double _statusRoom;

    public RowStack() => Loaded += (_, _) => KitStatus.Ask(this);

    /// <inheritdoc />
    public void StatusRoom(double width)
    {
        _statusRoom = double.IsFinite(width) ? Math.Max(0, width) : 0;
        ApplyRoom();
    }

    /// <summary>[S-12834] Give the first shown row down the stack the status's room at its end, the others their side inset.</summary>
    protected void ApplyRoom()
    {
        var first = Orientation == Orientation.Vertical;
        foreach (var row in Children.OfType<RowBlock>())
        {
            var shown = row.Visibility == Visibility.Visible;
            var padding = row.Padding with { Right = FamilySpace.Inset + (first && shown ? _statusRoom : 0) };
            if (row.Padding != padding)
            {
                row.Padding = padding;
            }

            first &= !shown;
        }
    }
}
