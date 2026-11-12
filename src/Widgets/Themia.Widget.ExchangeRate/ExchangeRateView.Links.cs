using Themia.Core.Links;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11912] The rate on its view's links: while a moment is pointed at on a history of the same pair, the
/// rate is that day's, with the day as its line under ([S-11914]); live again when the pointer goes.
/// </summary>
internal sealed partial class ExchangeRateView
{
    private readonly WidgetLinks _links = new();
    private InstantLink? _standsAt;

    private void FollowInstants() => _links.Follow(LinkTopics.HoveredInstant, moment =>
    {
        _standsAt = moment is not null && moment.Subject == LinkInstants.Subject("rate", $"{_config.EffectiveBase}/{_config.EffectiveQuote}") ? moment : null;
        Render();
    });

    /// <summary>The rate on the day pointed at and its caption, or null while the reading is live.</summary>
    private (string Rate, string Caption)? RateAtInstant(PairRate rate)
        => _standsAt is { } moment && rate.At(moment.At) is { } then
            ? (ExchangeRules.Format(then.Rate), LinkInstants.Caption(PairRate.Noon(then.Day, DateTimeOffset.Now.Offset), DateTimeOffset.Now, InstantCaption.Day))
            : null;
}
