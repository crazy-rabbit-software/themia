using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12830] [S-12831] Which elements are a widget's items: the boxes that tile its card (a row's band, a cell, a tile),
/// so the layout dump (<c>--render widget … --layout-json</c>) and the item check can see them. A family row marks itself,
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

    /// <summary>[S-16432] Take <paramref name="element"/>'s mark off again (no mark: nothing).</summary>
    public void Forget(FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        _marked.Remove(element);
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
/// [S-16514] A picture its widget draws to fill its box (a cover, a photo), so the layout dump (<c>--layout-json</c>'s
/// <c>pictures</c>) and the gap check know the face by what it draws, never by its kind's name: a picture that reaches the
/// card's edges is the card's top, foot and sides (the owner's exemption: "A kind whose face is a picture ... filling the
/// card edge to edge ... meets WR-1..WR-4 by that picture"); one inside the card is judged as any part.
/// </summary>
public static class PictureMark
{
    private static readonly MarkTable Pictures = new();

    /// <summary>Mark <paramref name="element"/> as a picture filling its box.</summary>
    public static void Mark(FrameworkElement element) => Pictures.Mark(element);

    /// <summary>Whether <paramref name="node"/> is a picture.</summary>
    public static bool IsPicture(DependencyObject? node) => Pictures.Has(node);
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
/// [S-14319] A running reading held to its widest form (<c>ReadoutBlock.Hold</c>): its box is the template's ink across,
/// so the layout dump (<c>--layout-json</c>'s <c>holds</c>) and the gap check judge its sides by the template's ink, the
/// place every value it shows stands in, not by the ink of the value it shows now (a "1:11" padded where "88:88" stands).
/// </summary>
public static class HoldMark
{
    private static readonly MarkTable Holds = new();

    /// <summary>Mark <paramref name="element"/> as a held reading's box.</summary>
    public static void Mark(FrameworkElement element) => Holds.Mark(element);

    /// <summary>[S-16432] <paramref name="element"/> holds no reading any more (its hold let go): judged by what it draws.</summary>
    public static void Forget(FrameworkElement element) => Holds.Forget(element);

    /// <summary>Whether <paramref name="node"/> is a held reading's box.</summary>
    public static bool IsHeld(DependencyObject? node) => Holds.Has(node);
}

/// <summary>
/// [S-12831] What an item does when the pointer is over it or presses it, for a render to show without a pointer
/// (<c>--render widget … --hover x,y</c>, <c>--pressed x,y</c>): the owner's rules on hover (WR-12: nothing changes size;
/// WR-13: the item under the pointer lights, edge to edge) are checked on those renders. An item that lights itself
/// registers its look here beside its pointer handlers.
/// </summary>
public static class PointerPreview
{
    private static readonly ConditionalWeakTable<UIElement, Action<bool>> Looks = [];

    /// <summary>[S-16341] The looks that depend on where the pointer is in the element (a graph's value under it).</summary>
    private static readonly ConditionalWeakTable<UIElement, Action<Windows.Foundation.Point, bool>> PointLooks = [];

    /// <summary>
    /// [S-16341] Register <paramref name="look"/> as what <paramref name="element"/> shows with the pointer at a point of it (in
    /// its own DIPs), pressed or not: a graph's value at that point.
    /// </summary>
    public static void Register(UIElement element, Action<Windows.Foundation.Point, bool> look)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(look);
        PointLooks.AddOrUpdate(element, look);
    }

    /// <summary>
    /// [S-16341] Show <paramref name="element"/>'s look with the pointer at <paramref name="root"/> (the window's content
    /// coordinates): a look that reads the point gets it in the element's own DIPs, any other its look as <see cref="Show(UIElement, bool)"/>.
    /// </summary>
    public static bool Show(UIElement element, Windows.Foundation.Point root, bool pressed)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (PointLooks.TryGetValue(element, out var at))
        {
            Windows.Foundation.Point local;
            try
            {
                local = element.TransformToVisual(null).Inverse.TransformPoint(root);
            }
            catch (ArgumentException)
            {
                // ignore: not in the window's tree any more; it shows nothing under a pointer it cannot be under
                return false;
            }

            at(local, pressed);
            return true;
        }

        return Show(element, pressed);
    }

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
        PointLooks.Remove(element);
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
