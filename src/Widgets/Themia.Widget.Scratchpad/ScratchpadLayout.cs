using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-1497] What the ink bar shows on a pad of a given size.</summary>
/// <param name="Bar">The bar along the pad's foot: the inks and the eraser.</param>
/// <param name="Nibs">The three nibs between them, when the width holds all.</param>
public readonly record struct InkBarPlan(bool Bar, bool Nibs);

/// <summary>
/// [S-1497] The ink bar's ladder, pure so each tier is pinned by a test: nothing on a tile Compact
/// either way (the pad is the widget); the six inks and the eraser from a Regular × Regular tile
/// whose width holds them inside the card's inset; the three nibs between them when the width holds all. Nothing more on a larger tile.
/// <para>
/// The bar is a row of 28 tools on the card's bottom inset ([S-6716]): the inks side by side, then
/// 8, the nibs side by side, then 8, the eraser.
/// </para>
/// </summary>
public static class ScratchpadLayout
{
    /// <summary>One tool of the bar: the family's icon button.</summary>
    public const double Tool = FamilyShape.IconButton;

    /// <summary>Between the bar's groups (the inks, the nibs, the eraser).</summary>
    public const double GroupGap = FamilySpace.S2;

    /// <summary>The inks, a gap and the eraser.</summary>
    public static double InksWidth(int inks) => (inks * Tool) + GroupGap + Tool;

    /// <summary>The nibs and the gap before them.</summary>
    public static double NibsWidth(int nibs) => (nibs * Tool) + GroupGap;

    public static InkBarPlan Bar(WidgetArea area, bool showTools, int inks, int nibs)
    {
        var width = area.Width - (2 * FamilySpace.Inset);

        // A Regular height (128 and up) always leaves the pad more than the bar's 32 and its inset.
        var bar = showTools
            && area.AtLeast(SizeClass.Regular, SizeClass.Regular)
            && width + Disclosure.Tolerance >= InksWidth(inks);

        return new InkBarPlan(bar, bar && width + Disclosure.Tolerance >= InksWidth(inks) + NibsWidth(nibs));
    }
}
