using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-6570] Whether a kind's view draws items at all: rows, tiles, chips or tracks painted through
/// the item layer (<c>ItemFill</c>, [S-6513]). A kind without items has nothing an item background
/// could reach, so the properties panel greys that row out for it ([S-6571]).
/// <para>
/// Stated by each widget's descriptor (<see cref="WidgetTraits.HasItems"/>, [S-6905]); it cannot
/// drift from the views: <c>ItemKindsTests</c> scans every view's source (and the shared parts it
/// uses) and fails when a kind that says it has none paints an item, or one that says it has some does not.
/// </para>
/// </summary>
public static class ItemKinds
{
    public static bool Has(WidgetId kind) => ModuleCatalog.Of(kind).Traits.HasFlag(WidgetTraits.HasItems);
}
