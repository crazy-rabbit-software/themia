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
}
