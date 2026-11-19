using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-13000] The config of a widget that holds real desktop items in a box of its own: a <b>fence</b>. The host's
/// desktop rules (<c>DesktopFences</c>, the reconcile, the undo snapshot) read and rewrite these without knowing the
/// module that draws it, as they do <see cref="IDesktopItemConfig"/>.
/// <para>
/// [S-13040] An item a fence holds is a desktop icon of the same view (one item widget, [S-13015]) that the fence lays on
/// a cell of the desktop's icon grid in its body; the keys are the desktop's (<see cref="DesktopItemKeys"/>), in the fence's
/// order, each once. It is never a free icon of that view besides ([S-13001]).
/// </para>
/// </summary>
public interface IHoldsDesktopItems
{
    /// <summary>The desktop items' keys, in the order the fence lays them in its cells.</summary>
    List<string> Items { get; set; }

    /// <summary>[S-13004] Which new desktop items arrive here instead of on the free desktop.</summary>
    FenceRuleSet Rules { get; }

    /// <summary>[S-13012] The fence shows its title bar alone.</summary>
    bool RolledUp { get; set; }

    /// <summary>[S-13012] Its height (a fraction of its display, as a widget's rect) while rolled up, to unroll to; 0 for none.</summary>
    double OpenHeight { get; set; }

    /// <summary>[S-13043] The first row of its items its body shows (whole rows: its cells stay on the grid); 0 for the top.</summary>
    int Scroll { get; set; }

    /// <summary>[S-13013] Its standing sort: the order it shows its items in; null for the order they were put in (a drop's place kept).</summary>
    Themia.Core.Desktop.DesktopSortBy? Sort { get; set; }

    /// <summary>[S-10910] Its title.</summary>
    string Name { get; set; }

    /// <summary>[S-13022] What it shows: what was put in, every desktop item its rules match, or files a search finds.</summary>
    FenceSource Source { get; set; }

    /// <summary>[S-13023] A search fence's place, recency and count (its rules are <see cref="Rules"/>).</summary>
    FenceQuery Query { get; set; }
}
