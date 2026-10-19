namespace Themia.Core.Model;

/// <summary>
/// [S-6573] A widget of a kind with items ([S-6570]) that draws them only in some of its styles or
/// states, or draws only tracks: its config says what it draws now, so the properties menu offers
/// Item background and Item text only where they reach something ([S-6571], [S-6574]). Implemented by
/// the config, so the rule is the widget's own and <see cref="Themia.Core.Widgets.ItemKinds"/> names no
/// kind. A config without it draws what its kind's trait says, text included.
/// </summary>
public interface IDrawsItems
{
    /// <summary>Whether the current style and state paint any item at all (a row, a tile, a chip or a track).</summary>
    bool DrawsItems => true;

    /// <summary>
    /// [S-6574] Whether any of those items carries text or a glyph the item text colour inks
    /// ([S-6546]): false where every item is a track (a ring, a seek bar, a meter's bars), which the
    /// item text never reaches.
    /// </summary>
    bool DrawsItemText => true;
}
