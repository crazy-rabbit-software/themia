using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-663] A view that lays itself out for the room it has: shows more as its tile grows, less as it
/// shrinks (see <see cref="WidgetArea"/> and <see cref="Disclosure"/>).
/// <para>
/// The card tells it, rather than every view wiring its own <c>SizeChanged</c> and each reading a
/// slightly different box — its own size, its root's, its root's less a padding it forgot to
/// subtract. The area is the view's own slot in the card: the card less its header, edge to edge,
/// before any inset the view applies itself.
/// </para>
/// <para>
/// Called once per layout pass that changed the area, after arrange — never for a size that has
/// not been measured, and never twice for the same size. The slot is a star row of a fixed-size
/// card, so nothing the view shows or hides in response can change the area again: there is no
/// loop to guard against. A view must also lay itself out on <c>ApplyConfig</c> and on new data
/// from the last area it was given, since those change what the ladder holds without a resize.
/// </para>
/// <para>
/// Optional. A view that does not implement it is sized exactly as before.
/// </para>
/// </summary>
public interface IWidgetSizeAware
{
    void ApplySize(WidgetArea area);
}
