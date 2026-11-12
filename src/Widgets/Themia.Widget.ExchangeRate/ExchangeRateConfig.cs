using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>
/// [S-5140] [S-11150] The ECB's reference rate of one pair as a reading: what one unit of the base
/// currency buys of the quote. Its 90 days are the <c>exchangeHistory</c> atom's.
/// </summary>
public sealed class ExchangeRateConfig : ExchangePairConfig, IContentAligned
{
    public override WidgetConfig Clone() => CopyPairTo(new ExchangeRateConfig());
}
