using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12400] [S-12410] [S-12807] A stack of a widget's parts that fills its room
/// (<see cref="ItemStretch.Reach(double, IReadOnlyList{StretchItem}, double, double)"/>): the parts that grow (a
/// family row, <see cref="IInked.Grows"/>) share the room their natural lengths leave, so they touch one another and
/// the stack's ends; a text, a readout or a control line keeps its length; with nothing that grows the parts stand
/// together, centred. A collapsed child takes no slot and no gap. Short of room it is a plain stack from its start.
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
    /// [S-12807] The rows the config states (0: the shown ones). Fewer shown leave the rest of those rows empty,
    /// at the foot ([S-12800]): the shown ones keep the places the stated rows reach the ends with.
    /// </summary>
    public int Rows
    {
        get => _rows;
        set
        {
            if (value != _rows)
            {
                _rows = Math.Max(0, value);
                InvalidateArrange();
            }
        }
    }

    private int _rows;

    /// <summary>
    /// [S-12814] With nothing that grows, the room goes between the parts (the first on the start, the last on the end;
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
        foreach (var child in Children)
        {
            child.Measure(room);
            if (child.Visibility != Visibility.Visible)
            {
                continue;
            }

            shown++;
            along += Down ? child.DesiredSize.Height : child.DesiredSize.Width;
            across = Math.Max(across, Down ? child.DesiredSize.Width : child.DesiredSize.Height);
        }

        along += Math.Max(0, shown - 1) * _spacing;
        return Down ? new Size(across, along) : new Size(along, across);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var shown = Children.Where(c => c.Visibility == Visibility.Visible).ToList();
        var items = shown.Select(Item).ToList();

        // Rows the config states but has nothing for stand in the lay as the shown ones' like, empty.
        while (items.Count > 0 && items.Count < _rows)
        {
            items.Add(items[^1]);
        }

        var room = Down ? finalSize.Height : finalSize.Width;
        var slots = ItemStretch.Reach(room, items, ItemGrids.Of(this), _spacing, _justify);

        // [S-12834] WR-6 (owner, 2026-11-19: "a widget that is 10 blocks hight should not show 4 items"): a row the room
        // does not hold whole is not shown, never cut by the card; the cells it would take are the foot's (WR-5). It is
        // laid in no room past the stack's end (clipped to nothing there), its own visibility left to its owner.
        var whole = shown.Select((_, i) => !items[i].Grows || slots[i].End <= room + LayoutMath.Tolerance).ToList();
        Laid = [.. shown.Select((child, i) => (child, slots[i])).Where((_, i) => whole[i])];
        for (var i = 0; i < shown.Count; i++)
        {
            shown[i].Arrange(!whole[i]
                ? new Rect(Down ? finalSize.Width : 0, Down ? 0 : finalSize.Height, 0, 0)
                : Down
                ? new Rect(0, slots[i].Start, finalSize.Width, slots[i].Length)
                : new Rect(slots[i].Start, 0, slots[i].Length, finalSize.Height));
        }

        foreach (var child in Children.Where(c => c.Visibility != Visibility.Visible))
        {
            child.Arrange(default);
        }

        return finalSize;
    }

    /// <summary>A child's natural length along the stack and whether it grows: a row down a stack does, a fixed size never.</summary>
    private StretchItem Item(UIElement child)
    {
        var natural = Down ? child.DesiredSize.Height : child.DesiredSize.Width;
        var own = child is FrameworkElement element ? (Down ? element.Height : element.Width) : double.NaN;
        return new StretchItem(natural, Down && !double.IsFinite(own) && child is IInked { Grows: true });
    }
}
