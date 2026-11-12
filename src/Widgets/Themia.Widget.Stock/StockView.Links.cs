using Themia.Core.Links;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11912] The stock's price on its view's links: while a moment is pointed at on a history of the same
/// symbol, the price is the bar's nearest it, with the day and time as its line under ([S-11914]); live
/// again when the pointer goes. A moment outside the bars this reading holds leaves it live.
/// </summary>
internal sealed partial class StockView
{
    private readonly WidgetLinks _links = new();
    private InstantLink? _standsAt;

    private void FollowInstants() => _links.Follow(LinkTopics.HoveredInstant, moment =>
    {
        _standsAt = moment is not null && moment.Subject == LinkInstants.Subject("stock", _config.EffectiveSymbol) ? moment : null;
        Render();
    });

    /// <summary>The price at the moment pointed at and its caption, or null while the reading is live.</summary>
    private (string Price, string Caption)? PriceAtInstant(TickerQuote quote)
    {
        if (_standsAt is not { } moment
            || LinkInstants.Nearest([.. quote.Bars.Select(bar => DateTimeOffset.FromUnixTimeSeconds(bar.Time))], moment.At) is not { } index)
        {
            return null;
        }

        var bar = quote.Bars[index];
        return (MarketQuote.Price(bar.Close, quote.PriceHint), LinkInstants.Caption(DateTimeOffset.FromUnixTimeSeconds(bar.Time), DateTimeOffset.Now, InstantCaption.DayAndTime));
    }
}
