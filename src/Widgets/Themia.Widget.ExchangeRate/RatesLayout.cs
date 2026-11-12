using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-5146] [S-11151] The rate's one layout and its size, on the family's blocks ([S-6702]): the rate a
/// Readout on the family's 28 at the content's top, start-aligned, the quote's code after it and the day
/// change beside it or, where they do not fit the card's width, on the line under it ([S-6707]); else
/// "per CHF" is that line. One height (the 28's line and its line under); 160..320 wide.
/// </summary>
public static class RatesLayout
{
    public const double Narrowest = 160;

    public const double Widest = 320;

    /// <summary>The widest change the rate prints, in the language's figures: the fold does not move with the rate.</summary>
    public static string WorstChange => RatesText.Change(88.88);

    /// <summary>[S-6707] Whether the code is drawn and where the change stands, for a content <paramref name="width"/> wide.</summary>
    public static ReadoutFold Fold(ReadoutText rate, double width) => ReadoutRules.Fold(FamilyType.Big28, rate, WorstChange, width);

    /// <summary>[S-11151] The rate's sizing, with its header.</summary>
    public static WidgetSizing Sizing()
    {
        var height = FamilySpace.ContentTop + FamilyType.Big28.LineHeight + FamilyType.Under(FamilyType.Big28).LineHeight + FamilySpace.Inset;
        return WidgetSizing.Range(new(Narrowest, height), new(200, height), new(Widest, height));
    }
}
