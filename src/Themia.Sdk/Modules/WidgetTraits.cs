namespace Themia.Sdk;

/// <summary>
/// [S-6905] Facts about a widget the shell asks without building it: what its card offers. A
/// descriptor states all of them (the member is required), so a new widget cannot inherit another's
/// by omission.
/// </summary>
[Flags]
public enum WidgetTraits
{
    None = 0,

    /// <summary>[S-6570] Its view paints items (rows, tiles, a meter's bars), so the item background and text apply.</summary>
    HasItems = 1,

    /// <summary>
    /// [S-6737] A one-readout widget (DECISIONS 2): its readout or glyph is the widget, so it alone may
    /// climb the Hero steps (96, 136, 192): the clock and the timers, the quick toggles and the power
    /// buttons. The render walk and <c>ReadoutRules.OneReadout</c> read it; no list of kinds is kept.
    /// </summary>
    OneReadout = 8,

    /// <summary>
    /// [S-5600] A new one starts on no card (its background Transparent, [S-6562]; the user can still give
    /// it one): what is drawn as a desktop icon, straight over the wallpaper as Windows draws its own (a
    /// desktop item, a shortcut).
    /// </summary>
    StartsTransparent = 16,

    /// <summary>
    /// [S-11880] It is never a card that joins its neighbours, whatever its fill (a desktop icon: icons
    /// touch on the widget grid, and each keeps its highlight's four corners). No group panel, no glass body.
    /// </summary>
    NeverJoins = 32,
}
