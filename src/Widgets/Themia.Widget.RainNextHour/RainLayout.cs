using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-5126] [S-11140] The rain atom's one layout and its size: the sentence on one Title line at the
/// content's top, its caption the Body line under it, then 8 down the bars (48 of them) with their time
/// line 4 under them. One height; 220..420 wide (the sentence and the caption end in their ellipsis
/// where the card is too narrow, the bars get wider).
/// </summary>
public static class RainLayout
{
    /// <summary>The bars' own height.</summary>
    public const double Bars = FamilyShape.IconHuge;

    /// <summary>Between the bars and their time line.</summary>
    public const double LabelGap = FamilySpace.S1;

    public const double Narrowest = 220;

    public const double Widest = 420;

    /// <summary>The chart: the bars, the gap and the time line.</summary>
    public static double Chart => Bars + LabelGap + FamilyType.Caption.LineHeight;

    /// <summary>
    /// [S-5126] Whether the window has rain to tell of (it starts, stops or goes on): only then the
    /// sentence, its caption and the bars; a dry window or one too short to speak for the hour is the
    /// Status row's one plain sentence.
    /// </summary>
    public static bool Warns(RainHeadline headline) => headline is RainHeadline.StartsIn or RainHeadline.StopsIn or RainHeadline.Continues;

    /// <summary>[S-11140] The rain's sizing, with its header.</summary>
    public static WidgetSizing Sizing()
    {
        var height = FamilySpace.ContentTop + FamilyType.Title.LineHeight + FamilyType.Body.LineHeight + FamilySpace.S2 + Chart + FamilySpace.Inset;
        return WidgetSizing.Range(new(Narrowest, height), new(300, height), new(Widest, height));
    }
}
