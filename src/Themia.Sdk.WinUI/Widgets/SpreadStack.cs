using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-14810] [S-12400] [S-12807] A stack of a widget's parts (<see cref="ItemSize.Lay"/>): a family row
/// (<see cref="IInked.IsRow"/>) stands at its one fixed size, its optimal, a text, a readout or a control line at its
/// own, one after another from the stack's start, touching (owner, 2026-11-20: "the widget items should actually have a
/// fixed size. then when you change the height of a widget the items should not grow or shrink to fit all the way from the
/// top to the bottom, they should just keep their size and the last item will be cutoff to some extent then."): the last
/// row the stack's end reaches is cut by it, and the room past the last part stays empty. Parts with no row among them
/// stand together, centred (or justified, a readout block's). A collapsed child takes no slot and no gap.
/// </summary>
public partial class SpreadStack : Panel
{
    private Orientation _orientation = Orientation.Vertical;
    private double _spacing;

    /// <summary>Down (the default) or across.</summary>
    public Orientation Orientation
    {
        get => _orientation;
        set
        {
            if (value != _orientation)
            {
                _orientation = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>The fixed gap between two items (a family token); the spread adds to it.</summary>
    public double Spacing
    {
        get => _spacing;
        set
        {
            if (value != _spacing)
            {
                _spacing = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>
    /// [S-12814] With no row among them, the room goes between the parts (the first on the start, the last on the end;
    /// one part takes it all) instead of round them: a readout block's parts ([S-12808]).
    /// </summary>
    public bool Justify
    {
        get => _justify;
        set
        {
            if (value != _justify)
            {
                _justify = value;
                InvalidateArrange();
            }
        }
    }

    private bool _justify;

    private bool Down => _orientation == Orientation.Vertical;

    /// <summary>The shown parts and the slots the last arrange gave them along the stack, first to last.</summary>
    public IReadOnlyList<(UIElement Part, ItemSlot Slot)> Laid { get; private set; } = [];

    protected override Size MeasureOverride(Size availableSize)
    {
        var along = 0.0;
        var across = 0.0;
        var shown = 0;
        var room = Down ? new Size(availableSize.Width, double.PositiveInfinity) : new Size(double.PositiveInfinity, availableSize.Height);
        var grid = ItemGrids.Of(this);
        foreach (var child in Children)
        {
            child.Measure(room);
            if (child.Visibility != Visibility.Visible)
            {
                continue;
            }

            shown++;
            var item = Item(child);
            along += item.IsRow ? ItemSize.Optimal(item.Natural, grid) : item.Natural;
            across = Math.Max(across, Down ? child.DesiredSize.Width : child.DesiredSize.Height);
        }

        along += Math.Max(0, shown - 1) * _spacing;
        return Down ? new Size(across, along) : new Size(along, across);
    }

    /// <summary>
    /// [S-14301] Where a shown part stands in its slot along the stack: the slot itself. A block's plan places its parts
    /// across instead (<see cref="ReadingFit"/>), once, in the one arrange.
    /// </summary>
    protected virtual Rect Lay(UIElement part, Rect slot) => slot;

    protected override Size ArrangeOverride(Size finalSize)
    {
        var shown = Children.Where(c => c.Visibility == Visibility.Visible).ToList();
        var room = Down ? finalSize.Height : finalSize.Width;
        var slots = ItemSize.Lay(room, [.. shown.Select(Item)], ItemGrids.Of(this), _spacing, _justify);

        // [S-14810] Every part at its slot; one past the stack's end is cut by it (the card's clip), never squeezed.
        Laid = [.. shown.Select((child, i) => (child, slots[i])).Where(laid => laid.Item2.Start < room - LayoutMath.Tolerance)];
        for (var i = 0; i < shown.Count; i++)
        {
            shown[i].Arrange(Lay(shown[i], Down
                ? new Rect(0, slots[i].Start, finalSize.Width, slots[i].Length)
                : new Rect(slots[i].Start, 0, slots[i].Length, finalSize.Height)));
        }

        foreach (var child in Children.Where(c => c.Visibility != Visibility.Visible))
        {
            child.Arrange(default);
        }

        return finalSize;
    }

    /// <summary>A child's natural length along the stack and whether it is a row: a family row down a stack is, a part of a fixed size never.</summary>
    private StackItem Item(UIElement child)
    {
        var natural = Down ? child.DesiredSize.Height : child.DesiredSize.Width;
        var own = child is FrameworkElement element ? (Down ? element.Height : element.Width) : double.NaN;
        return new StackItem(natural, Down && !double.IsFinite(own) && child is IInked { IsRow: true });
    }
}
