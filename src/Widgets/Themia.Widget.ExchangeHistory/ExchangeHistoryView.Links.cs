using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Core.Widgets;

namespace Themia.Widget.ExchangeHistory;

/// <summary>
/// [S-11910] [S-11911] The rate's history on its view's links: the day under the pointer is published as a moment of this
/// pair (<c>rate/chf/eur</c>, the day's noon), and a moment pointed at on any history chart of the view draws the hairline
/// at its day where the history holds it. The path is the price panel's (<see cref="Themia.Sdk.WinUI.Kit.PriceChart"/>,
/// [S-6756]).
/// </summary>
internal sealed partial class ExchangeHistoryView
{
    private readonly WidgetLinks _links = new();

    /// <summary>The rate drawn now, its days with it.</summary>
    private PairRate? _shownRate;

    private string Subject => LinkInstants.Subject("rate", $"{_config.EffectiveBase}/{_config.EffectiveQuote}");

    private void JoinInstants()
        => _chart.Moments(
            _links,
            LinkTopics.HoveredInstant,
            () => Subject,
            () => _shownRate?.Days is { Count: > 1 } days ? days.Count : 0,
            i => PairRate.Noon(_shownRate!.Days![i], DateTimeOffset.Now.Offset),
            DayAt);

    /// <summary>[S-11913] The day of the history <paramref name="at"/> stands on, or none outside it.</summary>
    private int? DayAt(DateTimeOffset at)
    {
        if (_shownRate is not { Days: { } days } rate || rate.At(at)?.Day is not { } day)
        {
            return null;
        }

        var index = days.ToList().IndexOf(day);
        return index >= 0 ? index : null;
    }
}
