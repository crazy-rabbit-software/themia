using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-1311] [S-11161] The price's one layout and its size, on the family's blocks ([S-6702], [S-6711]):
/// the price a Readout on the family's 28 at the content's top ([S-2449]), the currency after it and the
/// change beside it ([S-6707]: the currency gives way on a narrow card); the line under it says the
/// session where the config asks for it ([S-11800]). Then 8 down a fact row each for the holding and the
/// extended print where the config has them. One height by the rows; 240..360 wide.
/// </summary>
public static class StockLayout
{
    /// <summary>[S-11800] The narrowest card: a price of eight figures and the widest change still stand side by side.</summary>
    public const double Narrowest = 240;

    public const double Widest = 360;

    /// <summary>The widest change the price prints, in the language's figures: the fold does not move with the price.</summary>
    public static string WorstChange => MarketQuote.ChangePercent(88.88);

    /// <summary>[S-6707] Whether the currency is drawn and where the change stands, for a content <paramref name="width"/> wide.</summary>
    public static ReadoutFold Fold(ReadoutText price, double width) => ReadoutRules.Fold(FamilyType.Big28, price, WorstChange, width);

    /// <summary>[S-11161] The price's sizing, with its header: the reading, its session's line where asked for, and its rows.</summary>
    public static WidgetSizing Sizing(StockConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var rows = config.Rows;
        var height = FamilySpace.ContentTop + FamilyType.Big28.LineHeight
            + (config.ShowsSession ? FamilyType.Under(FamilyType.Big28).LineHeight : 0)
            + (rows > 0 ? FamilySpace.S2 + (rows * FamilyShape.Row1) : 0) + FamilySpace.Inset;
        return WidgetSizing.Range(new(Narrowest, height), new(Narrowest, height), new(Widest, height));
    }
}
