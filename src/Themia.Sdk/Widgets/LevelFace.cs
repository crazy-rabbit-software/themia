using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-2150] [S-11308] A level widget (the volume, the brightness): one atom, the level's Readout and the
/// Controls row (the slider, with its mute button for the volume), always both. The level is a Readout
/// at the view's top, start-aligned, on the data widgets' 28, sized on its widest case ("100%"); its line
/// under (the volume's output device) is always planned where the widget has one. The Controls row
/// stands 8 under it at the bottom inset. The card is that, one height, 160 to 360 wide: a wider card
/// gives the slider more travel and nothing else.
/// </summary>
public static class LevelFace
{
    /// <summary>The level's widest case: the Readout is sized on it, so a moving level never moves it.</summary>
    public static readonly ReadoutText Worst = new("100", "%");

    /// <summary>The level's step: the data widgets' number ([S-2220]).</summary>
    public static TypeStep Step => FamilyType.Big28;

    /// <summary>What the Controls row takes: its button's height and the 8 over it.</summary>
    public const double ControlsCost = FamilyShape.Button + FamilySpace.S2;

    /// <summary>[S-11308] The card's width: 160 to 360, a new one 200.</summary>
    public const double MinWidth = 160, NaturalWidth = 200, MaxWidth = 360;

    /// <summary>
    /// [S-11308] A level widget's sizing ([S-2438]), with its header (on by default): the content top, the
    /// Readout on 28 and its line under where <paramref name="under"/>, 8, the Controls row, the inset;
    /// one height, 160..360 wide.
    /// </summary>
    public static WidgetSizing Sizing(bool under)
    {
        var height = FamilySpace.ContentTop + ReadoutRules.Height(Step, Worst with { LineUnder = under }) + ControlsCost + FamilySpace.Inset;
        return WidgetSizing.Range(new SizeDip(MinWidth, height), new SizeDip(NaturalWidth, height), new SizeDip(MaxWidth, height));
    }
}
