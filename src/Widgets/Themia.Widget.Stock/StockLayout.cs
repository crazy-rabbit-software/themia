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

    /// <summary>
    /// [S-11161] [S-12481] The price's sizing: the reading, the line under it (always: it names the quote, its session
    /// after the name where asked for; the view holds its place, so the sizing counts it, [S-12800]) and its rows.
    /// </summary>
    public static WidgetSizing Sizing(StockConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var rows = config.Rows;
        var height = FamilySpace.ContentTop + FamilyType.Big28.LineHeight
            + FamilyType.Under(FamilyType.Big28).LineHeight
            + (rows > 0 ? FamilySpace.S2 + (rows * FamilyShape.Row1) : 0) + FamilySpace.Inset;
        var sizing = WidgetSizing.Range(new(Narrowest, height), new(Narrowest, height), new(Widest, height));

        // [S-12834] [S-14100] Over rows the price stands at its own lines from its first ink on the card's inset, grown to a
        // grid line (the view's band), and the rows grow from there to the card's foot together: the height steps by a
        // cell a row, so the rows fill it whole at every height (a new one the step nearest the stated height).
        return rows > 0
            ? sizing.WithHeightSteps(SizeSteps.Growing(FamilyShape.Row1, rows, MostRowSteps, PriceBand))
            : sizing;
    }

    /// <summary>[S-14100] How many cells more each row under the price may grow than its least: the height's steps.</summary>
    public const int MostRowSteps = 30;

    /// <summary>
    /// [S-12834] [S-14100] The price over rows as one length from the card's top edge: the inset less the value's ink over
    /// its capitals, its line and its line under (the view grows it to a grid line, <c>StockView.Place</c>).
    /// </summary>
    public static double PriceBand => FamilySpace.Inset - LineInk.Of(FamilyType.Big28).Over
        + FamilyType.Big28.LineHeight + FamilyType.Under(FamilyType.Big28).LineHeight;
}
