using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-11120] [S-11121] The moon's two looks and their sizes. Disc: a Readout of how much of the moon is
/// lit on the family's 28 ("23%"), the moon drawn at its phase as its lead at the step's size, the
/// phase's name the Caption under it: one height, 140..320 wide. Phase month: a disc a day of the month
/// holding now on a fixed grid of seven columns and five rows (31 days at most), a 20 disc on a 28 pitch,
/// the month's name the Caption 8 over it: one size. The rise, set and the next full and new moon are
/// the moon times' atom.
/// </summary>
public static class MoonLayout
{
    /// <summary>The widest share the moon prints, led by its disc, its phase's name under it.</summary>
    public static ReadoutText Worst(string full = "100%") => ReadoutRules.Split(full) with { Lead = true, LineUnder = true };

    /// <summary>[S-11121] A day's cell on the month's grid, and its disc in it (the gap round it holds today's ring).</summary>
    public const double Pitch = FamilyShape.Row1;

    public const double Disc = Pitch - FamilySpace.S2;

    public const int Columns = 7;

    public const int Rows = 5;

    public const double Narrowest = 140;

    public const double Widest = 320;

    /// <summary>[S-11121] The month's grid, its name over it: its width and height.</summary>
    public static SizeDip Month => new(Columns * Pitch, FamilyType.Caption.LineHeight + FamilySpace.S2 + (Rows * Pitch));

    /// <summary>[S-11121] The moon's sizing, with its header.</summary>
    public static WidgetSizing Sizing(MoonConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Style == MoonStyle.PhaseMonth)
        {
            return WidgetSizing.Fixed(new((2 * FamilySpace.Inset) + Month.Width, FamilySpace.ContentTop + Month.Height + FamilySpace.Inset));
        }

        var height = FamilySpace.ContentTop + ReadoutRules.Height(FamilyType.Big28, Worst()) + FamilySpace.Inset;
        return WidgetSizing.Range(new(Narrowest, height), new(160, height), new(Widest, height));
    }
}
