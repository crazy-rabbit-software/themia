using Microsoft.UI.Xaml;
using Themia.Core.Links;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11910] [S-11911] The stock's history on its view's links: the bar under the pointer is published as
/// a moment of this symbol (<c>stock/msft</c>), and a moment pointed at on any history chart of the view
/// draws the hairline at its nearest bar where the period holds it (no price label: that is the pointer's).
/// </summary>
internal sealed partial class StockHistoryView
{
    private readonly WidgetLinks _links = new();
    private InstantLink? _marked;

    private string Subject => LinkInstants.Subject("stock", _config.EffectiveSymbol);

    private void FollowInstants() => _links.Follow(LinkTopics.HoveredInstant, moment =>
    {
        _marked = moment;
        MarkInstant();
    });

    /// <summary>The pointer is on the bar of <paramref name="epochSeconds"/>.</summary>
    private void PublishInstant(long epochSeconds)
        => _links.Set(LinkTopics.HoveredInstant, new InstantLink(DateTimeOffset.FromUnixTimeSeconds(epochSeconds), Subject));

    private void UnpublishInstant() => _links.Set(LinkTopics.HoveredInstant, null);

    /// <summary>[S-11911] The hairline at the bar nearest the moment pointed at elsewhere, or none.</summary>
    private void MarkInstant()
    {
        if (_probe.Visibility == Visibility.Visible)
        {
            // The pointer is here: its own rule stands.
            return;
        }

        var index = _marked is { } moment && _quote?.Value is { } quote && _drawn.Count == quote.Bars.Count
            ? LinkInstants.Nearest([.. quote.Bars.Select(bar => DateTimeOffset.FromUnixTimeSeconds(bar.Time))], moment.At)
            : null;
        if (index is not { } i)
        {
            _rule.Visibility = Visibility.Collapsed;
            return;
        }

        _rule.X1 = _rule.X2 = _drawn[i].X;
        _rule.Y1 = 0;
        _rule.Y2 = _chart.ActualHeight;
        _rule.Visibility = Visibility.Visible;
    }
}
