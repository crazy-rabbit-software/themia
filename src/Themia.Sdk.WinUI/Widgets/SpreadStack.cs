using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12400] [S-12410] [S-12807] [S-12808] A stack of a widget's parts that reaches both of its ends
/// (<see cref="ItemStretch.Reach(double, IReadOnlyList{StretchItem}, double, double, double)"/>): the first
/// part's ink (<see cref="PartInk"/>: its capitals or its glyph, not its line's leading) stands on the stack's start
/// and the last part's on its end, the parts' own bands and leading reaching past them into the inset (so a stack
/// stands where its ink belongs: on the content's top and, with the foot's inset as its margin, on the foot's inset
/// line). The room between goes to the rows' bands, each stretching toward its own most (a family row: half again,
/// <see cref="RowBlock"/>'s <c>MaxHeight</c>; a fixed <c>Height</c>/<c>Width</c>, a text or a readout: none), and
/// what they cannot take is spacing between them. A collapsed child takes no slot and no gap. Only when the parts'
/// contents do not fit is it a plain stack from its start. A readout's parts (its reading, its lines) stand in one
/// so ([S-12808]).
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
    /// [S-12808] Whether it reaches its ends (the default). Off, it is a plain stack from its start, as tall as its
    /// parts: a readout's lines kept together until its view asks them to reach ([S-12808], <see cref="ReadoutBlock"/>).
    /// </summary>
    public bool Reaches
    {
        get => _reaches;
        set
        {
            if (value != _reaches)
            {
                _reaches = value;
                InvalidateMeasure();
            }
        }
    }

    private bool _reaches = true;

    private bool Down => _orientation == Orientation.Vertical;

    protected override Size MeasureOverride(Size availableSize)
    {
        var along = 0.0;
        var across = 0.0;
        var shown = new List<UIElement>();
        var room = Down ? new Size(availableSize.Width, double.PositiveInfinity) : new Size(double.PositiveInfinity, availableSize.Height);
        foreach (var child in Children)
        {
            child.Measure(room);
            if (child.Visibility != Visibility.Visible)
            {
                continue;
            }

            shown.Add(child);
            along += Down ? child.DesiredSize.Height : child.DesiredSize.Width;
            across = Math.Max(across, Down ? child.DesiredSize.Width : child.DesiredSize.Height);
        }

        // What the parts' ink spans: the first's band and leading and the last's stand outside the stack.
        if (shown.Count > 0 && _reaches)
        {
            along -= Band(shown[0]) + Band(shown[^1]);
            if (Down)
            {
                along -= PartInk.Of(shown[0]).Over + PartInk.Of(shown[^1]).Under;
            }
        }

        along += Math.Max(0, shown.Count - 1) * _spacing;
        along = Math.Max(0, along);
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

        var slots = _reaches ? ItemStretch.Reach(Down ? finalSize.Height : finalSize.Width, items, _spacing, scale: XamlRoot?.RasterizationScale ?? 1) : Stacked(items);
        for (var i = 0; i < shown.Count; i++)
        {
            shown[i].Arrange(Down
                ? new Rect(0, slots[i].Start, finalSize.Width, slots[i].Length)
                : new Rect(slots[i].Start, 0, slots[i].Length, finalSize.Height));
        }

        foreach (var child in Children.Where(c => c.Visibility != Visibility.Visible))
        {
            child.Arrange(default);
        }

        return finalSize;
    }

    /// <summary>The parts one after another from the start at their natural lengths, the spacing between them.</summary>
    private ItemSlot[] Stacked(List<StretchItem> items)
    {
        var slots = new ItemSlot[items.Count];
        var at = 0.0;
        for (var i = 0; i < items.Count; i++)
        {
            slots[i] = new ItemSlot(at, items[i].Natural);
            at += items[i].Natural + _spacing;
        }

        return slots;
    }

    /// <summary>A family row's band along a vertical stack; anything else has none.</summary>
    private double Band(UIElement child) => Down && child is IInked part ? part.Band : 0;

    /// <summary>A child's natural length along the stack, the most it stretches to and its band.</summary>
    private StretchItem Item(UIElement child)
    {
        var natural = Down ? child.DesiredSize.Height : child.DesiredSize.Width;
        if (child is not FrameworkElement element)
        {
            return StretchItem.Free(natural);
        }

        var own = Down ? element.Height : element.Width;
        var most = Down ? element.MaxHeight : element.MaxWidth;
        var margin = Down ? element.Margin.Top + element.Margin.Bottom : element.Margin.Left + element.Margin.Right;
        var band = Band(child);
        var ink = Down ? PartInk.Of(child) : LineInk.None;

        // A row stretches to its MaxHeight; a text, a readout, a control line never grow with the box ([S-12410]):
        // the room goes between them.
        return !double.IsFinite(own) && double.IsFinite(most) ? new StretchItem(natural, Math.Max(natural, most + margin), band, ink)
            : new StretchItem(natural, natural, band, ink);
    }
}
