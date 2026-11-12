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
    /// [S-3711] It may have a header at all. The quick-setting toggles never do ([S-3901]): the whole
    /// tile is the switch and its name is under the icon. Nor does the audio visualizer ([S-3200]):
    /// the spectrum is the widget, edge to edge.
    /// </summary>
    CanHaveHeader = 2,

    /// <summary>
    /// [S-700] A new one starts without its header (the user can still give it one): the clock, a
    /// single readout ([S-700]); the power buttons, whose name is under the icon ([S-1911]); the sun,
    /// the moon and the drive, which say what they are ([S-1161]); [S-2590] the alarm, the gallery and the
    /// video stream, whose time or pictures say what they are.
    /// </summary>
    StartsHeaderless = 4,

    /// <summary>
    /// [S-6737] A one-readout widget (DECISIONS 2): its readout or glyph is the widget, so it alone may
    /// climb the Hero steps (96, 136, 192): the clock and the timers, the quick toggles and the power
    /// buttons. The render walk and <c>ReadoutRules.OneReadout</c> read it; no list of kinds is kept.
    /// </summary>
    OneReadout = 8,
}
