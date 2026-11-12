using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Widget.MusicProgress;

/// <summary>
/// [S-11330] The progress atom's row: the elapsed time and the total at its two ends (Caption, tertiary,
/// in columns as wide as their widest case), the seek bar between them, 8 from each, one Caption line
/// tall in the card's inset. One height; a wider card only lengthens the bar.
/// </summary>
public static class MusicProgressFace
{
    /// <summary>[S-11330] The row's height: one Caption line.</summary>
    public static double Row => FamilyType.Caption.LineHeight;

    /// <summary>[S-11330] The card's width: 200 to 520, a new one 340.</summary>
    public const double Narrowest = 200, NaturalWidth = 340, Widest = 520;

    /// <summary>[S-11330] The card's height without a header: the inset, the row, the inset (40).</summary>
    public static double Height => FamilySpace.Inset + Row + FamilySpace.Inset;

    /// <summary>[S-11330] The progress atom's sizing ([S-2438]): one height, 200..520 wide; no header by default.</summary>
    public static WidgetSizing Sizing()
        => WidgetSizing.Range(new SizeDip(Narrowest, Height), new SizeDip(NaturalWidth, Height), new SizeDip(Widest, Height));

    /// <summary>[S-1475] A time column's width: its widest case, every figure an 8 ("8:88" for none yet).</summary>
    public static double TimeWidth(string total)
    {
        var widest = new string([.. (total ?? string.Empty).Select(c => char.IsDigit(c) ? '8' : c)]);
        return Math.Ceiling(HeroFit.Ems(widest.Length == 0 ? "8:88" : widest) * FamilyType.Caption.FontSize);
    }
}
