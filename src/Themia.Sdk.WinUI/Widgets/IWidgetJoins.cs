using Themia.Sdk.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12482] [S-12483] A view that lays its content out against the card's top and foot itself: told which of
/// them a member of its group joins (<see cref="JoinedSides"/>), so on such a side its content runs to the edge
/// and meets its mate's across the seam. The card insets a view's top itself (none where a mate joins); a view
/// held by <see cref="NaturalBlock"/> is stood against the joined side by it. This is for the rest: a view
/// that keeps a foot inset or centres a block of its own (<see cref="JoinedSides.Foot"/>,
/// <see cref="JoinedSides.Above"/>).
/// <para>
/// The card tells it when the view is attached and whenever the joins change (a group formed or left, a mate
/// moved). Optional: a view whose content already reaches its foot (a list's rows, a chart) does not need it.
/// </para>
/// </summary>
public interface IWidgetJoins
{
    void ApplyJoins(JoinedSides sides);
}
