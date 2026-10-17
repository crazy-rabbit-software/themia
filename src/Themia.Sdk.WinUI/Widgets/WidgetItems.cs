using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;

namespace Themia.App.Shell;

/// <summary>
/// [S-814] Which elements of a widget are its items (rows, tiles, cells), so the shell can tell a press
/// on one from a press on the widget's background — the one rule that clears a selection on a click
/// into empty space, for every widget. <see cref="Widgets.RowFocus.Prepare"/> marks every row that
/// carries a focus ring; a view whose items have none marks them here.
/// </summary>
public static class WidgetItems
{
    private static readonly ConditionalWeakTable<UIElement, object> Items = [];
    private static readonly object Marker = new();

    /// <summary>Mark <paramref name="element"/> as one of its widget's items.</summary>
    public static void Mark(UIElement element) => Items.AddOrUpdate(element, Marker);

    /// <summary>Whether <paramref name="element"/> was marked as an item.</summary>
    internal static bool IsItem(UIElement element) => Items.TryGetValue(element, out _);
}
