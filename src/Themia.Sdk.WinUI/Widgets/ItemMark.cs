using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12830] [S-12831] Which elements are a widget's items: the boxes that tile its card (a row's band, a cell, a tile),
/// so the layout dump (<c>--widget-png … --layout-json</c>) and the item check can see them. A family row marks itself,
/// an item filled through the item layer as a row or a tile is one, and a part that is an item by its own rule (a
/// month's day cell) marks itself here.
/// </summary>
public static class ItemMark
{
    private static readonly MarkTable Items = new();

    /// <summary>Mark <paramref name="element"/> as one of its widget's items.</summary>
    public static void Mark(FrameworkElement element) => Items.Mark(element);

    /// <summary>Whether <paramref name="node"/> is an item.</summary>
    public static bool IsItem(DependencyObject? node) => Items.Has(node);
}

/// <summary>
/// [S-12831] [S-14313] Which elements carry a mark the layout dump reads (an item, a plot, a free field): one table per
/// mark, keyed by the element. A plain XAML element's managed object may be collected and made anew while the element
/// lives (a list's heading no view holds, the screen-time chart's bars' grid), and the new one is not the key this table
/// holds: the mark went unseen (tasks' done heading read as a gap of a row; the screen-time chart's plot missing from two
/// of three cards, its bars then judged as a reading off centre). A handler of the element's own that holds it keeps that
/// object for as long as the element lives.
/// </summary>
internal sealed class MarkTable
{
    private static readonly object Marker = new();
    private readonly ConditionalWeakTable<FrameworkElement, object> _marked = [];

    public void Mark(FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (_marked.TryGetValue(element, out _))
        {
            return;
        }

        _marked.Add(element, Marker);
        element.Loaded += (_, _) => GC.KeepAlive(element);
    }

    public bool Has(DependencyObject? node) => node is FrameworkElement element && _marked.TryGetValue(element, out _);
}

/// <summary>
/// [S-6754] A graph's plot drawn off the one <c>Chart</c> (the screen-time chart's bars), so the layout dump
/// (<c>--layout-json</c>'s <c>plots</c>) and the gap check judge it as a chart's: it reaches the card's edges.
/// </summary>
public static class PlotMark
{
    private static readonly MarkTable Plots = new();

    /// <summary>Mark <paramref name="element"/> as its widget's plot.</summary>
    public static void Mark(FrameworkElement element) => Plots.Mark(element);

    /// <summary>Whether <paramref name="node"/> is a plot.</summary>
    public static bool IsPlot(DependencyObject? node) => Plots.Has(node);
}

/// <summary>
/// [S-14314] A column's head over a column of items (the month's weekday letters over its day cells): its box is the
/// column's across, so the layout dump (<c>--layout-json</c>'s <c>heads</c>) and the gap check read the text in it against
/// a card edge that box stands on as an item's: the column's own padding stands there in the inset's place (WR-3), the
/// head's letters on the same line as the numbers in the cells under them. It is no item: it is not one of the items
/// that tile the card alike (WR-6), takes no pointer and is not judged by the item check.
/// </summary>
public static class HeadMark
{
    private static readonly MarkTable Heads = new();

    /// <summary>Mark <paramref name="element"/> as a column's head.</summary>
    public static void Mark(FrameworkElement element) => Heads.Mark(element);

    /// <summary>Whether <paramref name="node"/> is a column's head.</summary>
    public static bool IsHead(DependencyObject? node) => Heads.Has(node);
}

/// <summary>
/// [S-12831] What an item does when the pointer is over it or presses it, for a render to show without a pointer
/// (<c>--widget-png … --hover x,y</c>, <c>--pressed x,y</c>): the owner's rules on hover (WR-12: nothing changes size;
/// WR-13: the item under the pointer lights, edge to edge) are checked on those renders. An item that lights itself
/// registers its look here beside its pointer handlers.
/// </summary>
public static class PointerPreview
{
    private static readonly ConditionalWeakTable<UIElement, Action<bool>> Looks = [];

    /// <summary>Register <paramref name="look"/> (pressed or not) as what <paramref name="element"/> shows under the pointer.</summary>
    public static void Register(UIElement element, Action<bool> look)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(look);
        Looks.AddOrUpdate(element, look);
    }

    /// <summary>Forget <paramref name="element"/>'s look.</summary>
    public static void Forget(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        Looks.Remove(element);
    }

    /// <summary>Show <paramref name="element"/>'s look under the pointer, if it has one.</summary>
    public static bool Show(UIElement element, bool pressed)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!Looks.TryGetValue(element, out var look))
        {
            return false;
        }

        look(pressed);
        return true;
    }
}
