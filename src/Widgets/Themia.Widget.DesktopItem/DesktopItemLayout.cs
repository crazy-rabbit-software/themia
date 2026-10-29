using Themia.Sdk.Kit;

namespace Themia.Widget.DesktopItem;

/// <summary>[S-5601] How much of an item fits its cell: the icon's size and how many lines of its name.</summary>
public readonly record struct ItemFit(double Icon, int Lines);

/// <summary>
/// [S-5601] The item widget's ladder, concise first ([S-666]): the icon over its name, as Explorer's
/// desktop draws an icon. Measured on the cell less an 8 inset (no card to stand off): a 48 icon
/// with two name lines at the default cell, one line, a 32 icon with one, then the icon alone. A
/// larger cell keeps the 48 icon and two lines centred; the room left is empty, as it is round a
/// desktop icon.
/// </summary>
public static class DesktopItemLayout
{
    /// <summary>The cell's edge to the icon and the name.</summary>
    public const double Inset = FamilySpace.S2;

    /// <summary>The icon to the name.</summary>
    public const double Gap = FamilySpace.S1;

    /// <summary>The narrowest a name is worth showing in.</summary>
    public const double MinNameWidth = FamilyShape.IconLarge;

    private static readonly ItemFit[] Ladder =
    [
        new(FamilyShape.IconHuge, 2),
        new(FamilyShape.IconHuge, 1),
        new(FamilyShape.IconLarge, 1),
        new(FamilyShape.IconHuge, 0),
        new(FamilyShape.IconLarge, 0),
    ];

    /// <summary>The first rung of the ladder that fits a <paramref name="width"/> × <paramref name="height"/> DIP cell; a 16 icon alone below them all.</summary>
    public static ItemFit For(double width, double height)
    {
        var room = (Width: width - (2 * Inset), Height: height - (2 * Inset));
        foreach (var fit in Ladder)
        {
            var tall = fit.Icon + (fit.Lines > 0 ? Gap + (fit.Lines * FamilyType.CaptionLine) : 0);
            var wide = fit.Lines > 0 ? Math.Max(fit.Icon, MinNameWidth) : fit.Icon;
            if (tall <= room.Height + 0.01 && wide <= room.Width + 0.01)
            {
                return fit;
            }
        }

        return new ItemFit(FamilyShape.Icon, 0);
    }
}
