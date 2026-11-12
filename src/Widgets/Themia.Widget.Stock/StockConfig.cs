using System.Text.Json.Serialization;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>
/// [S-11160] One instrument's price as a reading: the price, its change, the session; a row for the
/// holding where one is set and a row for the extended-hours print where it is asked for. Its chart is
/// the <c>stockHistory</c> atom's. The instrument and its period are the SDK's <see cref="StockQuoteConfig"/>.
/// </summary>
public sealed class StockConfig : StockQuoteConfig, IContentAligned
{
    /// <summary>Say so when the listing exchange is shut. Crypto is never marked: it trades all week.</summary>
    public bool ShowMarketState { get; set; } = true;

    /// <summary>
    /// [S-2776] How many of the instrument the user owns, for the holding's row under the price. Null
    /// (the default) is none and no row; a stored 0, negative or unusable amount is repaired to none at
    /// load (<see cref="StockHolding.Normalise"/>).
    /// </summary>
    public double? SharesOwned { get; set; }

    /// <summary>[S-11161] The rows under the price: the holding where one is set, the extended print where it is asked for.</summary>
    [JsonIgnore]
    public int Rows => (SharesOwned is not null ? 1 : 0) + (WantsPrePost ? 1 : 0);

    /// <summary>An unusable amount owned goes to none, logged.</summary>
    public override IReadOnlyList<string> Repair()
    {
        SharesOwned = StockHolding.Normalise(SharesOwned, out var problem);
        return problem is null ? [] : [problem];
    }

    public override WidgetConfig Clone() => CopyQuoteTo(new StockConfig
    {
        ShowMarketState = ShowMarketState,
        SharesOwned = SharesOwned,
    });
}
