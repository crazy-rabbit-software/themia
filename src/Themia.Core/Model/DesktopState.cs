namespace Themia.Core.Model;

/// <summary>
/// [S-5500] What the desktop shows while Themia runs: ⚙'s one choice. Stored as its number; an
/// unknown number is repaired to <see cref="WindowsIcons"/> at load.
/// </summary>
public enum DesktopShows
{
    /// <summary>Explorer's own icons, as Windows has them: the desktop as it was before Themia ([S-5505]).</summary>
    WindowsIcons = 0,

    /// <summary>Themia's item widgets; Explorer's icons hidden for the session ([S-5501], [S-5503]).</summary>
    ThemiaItems = 1,

    /// <summary>No icons at all: Explorer's hidden for the session, no item widgets ([S-5506]).</summary>
    Nothing = 2,
}

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
