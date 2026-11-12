using Themia.Core.Model;

namespace Themia.Widget.ExchangeHistory;

/// <summary>[S-11155] One pair's last 90 days as a chart: the pair is the rate atom's (<see cref="ExchangePairConfig"/>).</summary>
public sealed class ExchangeHistoryConfig : ExchangePairConfig
{
    public override WidgetConfig Clone() => CopyPairTo(new ExchangeHistoryConfig());
}
