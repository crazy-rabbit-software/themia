using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Widget.DesktopItem;

/// <summary>[S-5601] What an item's cell draws: the icon's size and how many lines of its name.</summary>
public readonly record struct ItemFit(double Icon, int Lines);

/// <summary>
/// [S-5601] [S-11204] The item widget's one cell, as Explorer's desktop draws an icon: an 8 inset (no
/// card to stand off), the 48 icon, 4, the name's Caption lines (the config's, 1 or 2) and the inset
/// again; 90 wide. One fixed size per its lines: nothing in it follows a box.
/// </summary>
public static class DesktopItemLayout
{
    /// <summary>The cell's edge to the icon and the name.</summary>
    public const double Inset = FamilySpace.S2;

    /// <summary>The icon to the name.</summary>
    public const double Gap = FamilySpace.S1;

    /// <summary>The narrowest a name is worth showing in.</summary>
    public const double MinNameWidth = FamilyShape.IconLarge;

    /// <summary>[S-11204] The name's lines: 1 or 2, a new one 2.</summary>
    public static int ClampLines(int lines) => Math.Clamp(lines, 1, 2);

    /// <summary>[S-11204] What the cell draws for <paramref name="lines"/> name lines: the 48 icon and them.</summary>
    public static ItemFit Fit(int lines) => new(FamilyShape.IconHuge, ClampLines(lines));

    /// <summary>[S-11204] The one cell for <paramref name="lines"/> name lines: 90 × 100 with two, 90 × 84 with one.</summary>
    public static SizeDip Size(int lines)
        => new(DesktopItemModule.CellWidth, (2 * Inset) + FamilyShape.IconHuge + Gap + (ClampLines(lines) * FamilyType.CaptionLine));
}
