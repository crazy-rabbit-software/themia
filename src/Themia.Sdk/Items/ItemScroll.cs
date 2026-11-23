using Themia.Core.Widgets;

namespace Themia.Sdk.Items;

/// <summary>
/// [S-16955] [S-16591] An item list's items past what the card shows are scrolled to by whole items (owner, 2026-11-23, of the forecast: "the weather
/// forecast lists are not scrollable although they should be"): rows down, columns across, a grid's lines down. Each item
/// keeps its one fixed size ([S-14810]); the first one shown is whole at the card's edge, the last one the card
/// reaches cut by it, and the last turn stands the last at the far edge. Pure, so the steps and the limits are tested
/// here; the view only turns them into its scroller's offset.
/// </summary>
public static class ItemScroll
{
    /// <summary>One wheel notch (Windows' WHEEL_DELTA): an item. A touchpad's smaller turns add up to one.</summary>
    public const int Notch = 120;

    /// <summary>
    /// The furthest first item of <paramref name="items"/> items <paramref name="item"/> long each in a viewport
    /// <paramref name="viewport"/> long: the step that brings the last item to the far edge (0: they all show).
    /// </summary>
    public static int LastFirst(int items, double item, double viewport)
    {
        if (items <= 0 || !(item > 0) || !double.IsFinite(item) || !double.IsFinite(viewport))
        {
            return 0;
        }

        var over = (items * item) - Math.Max(0, viewport);
        return over > LayoutMath.Tolerance ? Math.Min(items - 1, (int)Math.Ceiling((over - LayoutMath.Tolerance) / item)) : 0;
    }

    /// <summary>
    /// The first item after <paramref name="by"/> items on from <paramref name="first"/> (negative: back), kept between the
    /// first item and <see cref="LastFirst"/>.
    /// </summary>
    public static int Step(int first, int by, int items, double item, double viewport)
        => Math.Clamp(first + by, 0, LastFirst(items, item, viewport));

    /// <summary>
    /// A wheel turn of <paramref name="delta"/> (positive: up or left, as Windows reports it) with the
    /// <paramref name="carry"/> left from the last: an item for every notch, the rest carried to the next turn; at either
    /// limit the rest is dropped, so a turn back moves at once.
    /// </summary>
    public static (int First, int Carry) Wheel(int first, int carry, int delta, int items, double item, double viewport)
    {
        var total = carry + delta;
        var notches = total / Notch;
        var next = Step(first, -notches, items, item, viewport);
        var rest = total % Notch;
        var atLimit = (rest < 0 && next >= LastFirst(items, item, viewport)) || (rest > 0 && next <= 0);
        return (next, atLimit ? 0 : rest);
    }

    /// <summary>
    /// The scroller's offset for <paramref name="first"/>: that item's start, never past the end of the items (the last turn
    /// stands the last item at the far edge).
    /// </summary>
    public static double Offset(int first, int items, double item, double viewport)
    {
        if (!(item > 0) || !double.IsFinite(item))
        {
            return 0;
        }

        var end = Math.Max(0, (Math.Max(0, items) * item) - Math.Max(0, double.IsFinite(viewport) ? viewport : 0));
        return Math.Clamp(Math.Max(0, first) * item, 0, end);
    }

    /// <summary>How many whole items a viewport holds: a page's step (at least one).</summary>
    public static int Page(double item, double viewport)
        => item > 0 && double.IsFinite(item) && double.IsFinite(viewport) ? Math.Max(1, (int)Math.Floor((viewport + LayoutMath.Tolerance) / item)) : 1;
}
