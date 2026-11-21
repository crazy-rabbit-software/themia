using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>[S-13000] One fence of a view: its widget and the config that holds its items.</summary>
public readonly record struct FenceOn(WidgetInstance Widget, IHoldsDesktopItems Fence);

/// <summary>
/// [S-13000]..[S-13014] [S-13040]..[S-13044] The fences' rules, pure (the owner, 2026-11-19: "a group of icons with a
/// header … build as a widget that can be created like fences does it"; and "the fence should basically just be a helper
/// to align the icon on the underlying engine without this transition in between"). A fence is a widget whose config holds
/// real desktop items by their keys (<see cref="IHoldsDesktopItems"/>); it lives on one view. On that view each item it
/// holds is a desktop icon like any other (its item widget, [S-13015]) that the fence lays on a cell of the desktop icon
/// grid in its body (<see cref="Lay"/>, <see cref="FenceCells"/>): the fence groups and constrains those cells, it draws no
/// item of its own. Which free icons a view shows (All, Chosen, None, [S-5525]) concerns the free desktop alone: an item in
/// a fence is always shown with its fence. Nothing here touches a file.
/// </summary>
public static partial class DesktopFences
{
    /// <summary>The fences of <paramref name="view"/>, in its widgets' order (the order a rule is looked for in).</summary>
    public static IEnumerable<FenceOn> On(ViewState view)
    {
        ArgumentNullException.ThrowIfNull(view);
        foreach (var widget in view.Widgets)
        {
            if (widget.Config is IHoldsDesktopItems fence)
            {
                yield return new FenceOn(widget, fence);
            }
        }
    }
}
