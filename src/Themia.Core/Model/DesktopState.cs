namespace Themia.Core.Model;

/// <summary>
/// [S-5522] What the desktop layer remembers across runs: the keys of every desktop item the
/// reconcile has seen, so a file that arrived while Themia was not running still lands
/// ([S-5520]) and an item the user removed from every view stays removed ([S-5524]).
/// </summary>
public sealed class DesktopState
{
    /// <summary>The known items' keys ([S-5510]), distinct ignoring case.</summary>
    public List<string> Known { get; set; } = [];

    /// <summary>
    /// [S-5560] The item widgets laid into the primary display's view while the display whose view held
    /// their items was unplugged, each with the cell it was given; null (not written) when there is none.
    /// </summary>
    public List<DesktopFallback>? Fallbacks { get; set; }

    /// <summary>
    /// [S-5596] What the desktop's icons were last sorted by (Sort by ›): the line that carries
    /// Explorer's dot. Name in a new state, the order new items are laid out in ([S-5521]).
    /// </summary>
    public Themia.Core.Desktop.DesktopSortBy Sort { get; set; }

    /// <summary>[S-5596] Bring a read desktop memory back inside its invariants: a number that names no sort is the order new items are laid out in.</summary>
    public IReadOnlyList<string> Repair()
    {
        if (Enum.IsDefined(Sort))
        {
            return [];
        }

        Sort = Themia.Core.Desktop.DesktopSortBy.Name;
        return ["unknown desktop sort; by name"];
    }
}

/// <summary>
/// [S-5560] One stand-in item widget: the widget (its id, in the view <see cref="View"/>) that shows
/// <see cref="Item"/> while the display whose view holds it is unplugged, and the cell it was given
/// there. It goes again when that view shows the item once more, unless the user has moved it since.
/// </summary>
public sealed class DesktopFallback
{
    /// <summary>The stand-in widget's id.</summary>
    public string Widget { get; set; } = string.Empty;

    /// <summary>The view it was laid into: the primary display's then.</summary>
    public string View { get; set; } = string.Empty;

    /// <summary>The item's key ([S-5510]).</summary>
    public string Item { get; set; } = string.Empty;

    /// <summary>The cell it was given: moved off it, the widget is the user's own.</summary>
    public Themia.Core.Geometry.FracRect Rect { get; set; }
}
