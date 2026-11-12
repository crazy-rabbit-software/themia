using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-1311] [S-11161] The price's one layout and its size, on the family's blocks ([S-6702], [S-6711]):
/// the price a Readout on the family's 28 at the content's top ([S-2449]), the currency after it and the
/// change beside it or, where they do not fit the card's width, on the line under it ([S-6707]); else
/// the line under says the session. Then 8 down a fact row each for the holding and the extended print
/// where the config has them. One height by the rows; 200..360 wide.
/// </summary>
public static class StockLayout
{
    public const double Narrowest = 200;

    public const double Widest = 360;

    /// <summary>The widest change the price prints, in the language's figures: the fold does not move with the price.</summary>
    public static string WorstChange => MarketQuote.ChangePercent(88.88);

    /// <summary>[S-6707] Whether the currency is drawn and where the change stands, for a content <paramref name="width"/> wide.</summary>
    public static ReadoutFold Fold(ReadoutText price, double width) => ReadoutRules.Fold(FamilyType.Big28, price, WorstChange, width);

    /// <summary>[S-11161] The price's sizing, with its header: the reading, its line under and its rows.</summary>
    public static WidgetSizing Sizing(StockConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var rows = config.Rows;
        var height = FamilySpace.ContentTop + FamilyType.Big28.LineHeight + FamilyType.Under(FamilyType.Big28).LineHeight
            + (rows > 0 ? FamilySpace.S2 + (rows * FamilyShape.Row1) : 0) + FamilySpace.Inset;
        return WidgetSizing.Range(new(Narrowest, height), new(240, height), new(Widest, height));
    }
}
