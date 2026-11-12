using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-1497] [S-11205] The scratchpad's sizes: the pad takes whatever the card has; the ink bar is a
/// setting (Ink bar, on by default), a row of 28 tools on the card's bottom inset ([S-6716]) of one
/// height at every size: the inks side by side, then 8, the nibs side by side, then 8, the eraser. It
/// is whole or not there: no tier drops a part of it, so a pad with the bar is never narrower than it.
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

    /// <summary>[S-11205] The whole bar's width: the inks, the nibs and the eraser with their gaps.</summary>
    public static double BarWidth(int inks, int nibs) => InksWidth(inks) + NibsWidth(nibs);

    /// <summary>[S-11205] The least pad without the bar, the widest and tallest a pad grows.</summary>
    public static readonly SizeDip Least = new(200, 160);

    public static readonly SizeDip Largest = new(800, 600);

    /// <summary>
    /// [S-11205] A free range, 200..800 × 160..600 (a new one 320 × 260); with the ink bar on, never
    /// narrower than the whole bar in the card's inset (6 inks, 3 nibs: 320).
    /// </summary>
    public static WidgetSizing Sizing(ScratchpadConfig config, int inks, int nibs)
    {
        ArgumentNullException.ThrowIfNull(config);
        var least = config.ShowTools ? Math.Max(Least.Width, BarWidth(inks, nibs) + (2 * FamilySpace.Inset)) : Least.Width;
        return WidgetSizing.Range(new(least, Least.Height), new(Math.Max(least, 320), 260), Largest);
    }
}
