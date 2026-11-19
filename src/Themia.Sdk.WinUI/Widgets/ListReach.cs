using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12807] A list laid by the reach rule (<see cref="ItemStretch.Rows"/>): its part (a scroll, a page, a grid)
/// starts its first row's band and leading over its room (a margin of <c>-</c><see cref="RowReach.Over"/>), so that
/// row's ink stands on the inset, and its rows stand <see cref="RowReach.Gap"/> apart once they are at their cap. The one
/// place a list sets those lengths. They are the rule's, not literals: the family's render walk ([S-6731])
/// judges the rule (its tests) and leaves an element laid here (<see cref="IsLaid"/>) out of its space check.
/// <para>
/// The mark is the element's <c>Name</c>, which lives in the element itself: a walk reaches a plain Grid or
/// StackPanel through the visual tree as another managed wrapper, so a table keyed by the wrapper would miss it.
/// </para>
/// </summary>
public static class ListReach
{
    /// <summary>The name an element laid by the rule carries.</summary>
    public const string Mark = "ListReach";

    /// <summary>Lay <paramref name="part"/> and its <paramref name="rows"/> panel (a stack's spacing or a grid's row spacing) as <paramref name="reach"/> says.</summary>
    public static void Apply(FrameworkElement part, RowReach reach, Panel? rows = null)
    {
        ArgumentNullException.ThrowIfNull(part);
        Shift(part, -reach.Over);
        switch (rows)
        {
            case StackPanel stack:
                stack.Spacing = reach.Gap;
                stack.Name = Mark;
                break;
            case Grid grid:
                grid.RowSpacing = reach.Gap;
                grid.Name = Mark;
                break;
        }
    }

    /// <summary>Stand <paramref name="element"/> <paramref name="top"/> from where its room starts (a week of the month after the room over it).</summary>
    public static void Shift(FrameworkElement element, double top)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.Margin = new Thickness(0, top, 0, 0);
        element.Name = Mark;
    }

    /// <summary>Stand <paramref name="element"/> <paramref name="bottom"/> over its room's end (a row under a list, its ink on the foot's inset).</summary>
    public static void Foot(FrameworkElement element, double bottom)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.Margin = new Thickness(0, 0, 0, bottom);
        element.Name = Mark;
    }

    /// <summary>Whether <paramref name="element"/>'s margin and spacing are the reach rule's.</summary>
    public static bool IsLaid(FrameworkElement element) => element?.Name == Mark;
}
