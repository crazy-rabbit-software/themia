using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-6570] Whether a kind's view draws items at all: rows, tiles, chips or tracks painted through
/// the item layer (<c>ItemFill</c>, [S-6513]). A kind without items has nothing an item background
/// could reach, so the properties menu leaves that line out for it ([S-6571]).
/// <para>
/// Stated by each widget's descriptor (<see cref="WidgetTraits.HasItems"/>, [S-6905]); it cannot
/// drift from the views: <c>ItemKindsTests</c> scans every view's source (and the shared parts it
/// uses) and fails when a kind that says it has none paints an item, or one that says it has some does not.
/// [S-6573] A widget of such a kind may still draw none in its current style or state, or only
/// tracks ([S-6574]): its config says so (<see cref="IDrawsItems"/>).
/// </para>
/// </summary>
public static class ItemKinds
{
    public static bool Has(WidgetId kind) => ModuleCatalog.Of(kind).Traits.HasFlag(WidgetTraits.HasItems);

    /// <summary>[S-6573] Whether <paramref name="widget"/> paints items in the style and state it has now.</summary>
    public static bool Draws(WidgetInstance widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        return Has(widget.Kind) && (widget.Config is not IDrawsItems own || own.DrawsItems);
    }

    /// <summary>[S-6574] Whether the item text colour reaches anything on <paramref name="widget"/> now: items that are not all tracks.</summary>
    public static bool DrawsText(WidgetInstance widget)
        => Draws(widget) && (widget.Config is not IDrawsItems own || own.DrawsItemText);
}
