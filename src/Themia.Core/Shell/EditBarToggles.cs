using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Settings;

namespace Themia.Core.Shell;

/// <summary>
/// [S-2330] The edit bar's toggles at its root (owner, 2026-11-15: "widgets can go over the edge toggle
/// should be integrated on the root level of the topbar"): Over the edge ([S-6608]) and Keep widgets from
/// overlapping ([S-11959]; owner, 2026-11-16: "is the collision feature implemented? i cannot see the toggle
/// button anywhere"), each its setting's one place ([S-3590]): no page sets them. There is no Snap to grid toggle: the grid is always on ([S-06],
/// owner 2026-11-15: "it should also not be possible to disable the grid anymore"). Lines like a page's,
/// so the guards walk them as one more level; the bar draws each as an icon toggle, its words the
/// tooltip, lit while on.
/// </summary>
public static class EditBarToggles
{
    /// <summary>The key of Over the edge.</summary>
    public const string OverEdge = "overEdge";

    /// <summary>[S-11959] The key of Keep widgets from overlapping.</summary>
    public const string Collisions = "collisions";

    /// <summary>How many toggles stand at the bar's root: their room is always kept, so the bar keeps one width.</summary>
    public const int Count = 2;

    /// <summary>The toggles in the bar's order, over <paramref name="host"/>'s state as it is now.</summary>
    public static IReadOnlyList<SettingsToggle> Of(ISettingsMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        var design = host.State.Design;
        return
        [
            new SettingsToggle(OverEdge, Str.Shell_Toolbar_OverEdgeName.Text(), Glyphs.FullScreen, design.OverEdge, on => host.Mutate(s => s.Design.OverEdge = on)),
            new SettingsToggle(Collisions, Str.Shell_Toolbar_CollisionsName.Text(), Glyphs.KeepApart, design.WidgetCollisions, on => host.Mutate(s => s.Design.WidgetCollisions = on, relayout: false)),
        ];
    }
}
