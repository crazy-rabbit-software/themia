using Themia.Core.Links;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11910] [S-11911] The stock's history on its view's links: the bar under the pointer is published as a moment of this
/// symbol (<c>stock/msft</c>), and a moment pointed at on any history chart of the view draws the hairline at its nearest
/// bar where the period holds it (no price readout: that is the pointer's). The path is the price panel's
/// (<see cref="Themia.Sdk.WinUI.Kit.PriceChart"/>, [S-6756]).
/// </summary>
internal sealed partial class StockHistoryView
{
    private readonly WidgetLinks _links = new();

    private string Subject => LinkInstants.Subject("stock", _config.EffectiveSymbol);

    /// <summary>[S-11913] The bar nearest <paramref name="at"/>, or none outside the period.</summary>
    private int? BarAt(DateTimeOffset at)
        => _quote?.Value is { } quote ? LinkInstants.Nearest([.. quote.Bars.Select(bar => DateTimeOffset.FromUnixTimeSeconds(bar.Time))], at) : null;
}
