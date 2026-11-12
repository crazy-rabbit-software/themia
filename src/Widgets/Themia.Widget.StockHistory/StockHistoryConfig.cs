using Themia.Core.Model;

namespace Themia.Widget.StockHistory;

/// <summary>[S-11165] One instrument's period as a chart: the instrument and its period are the price atom's (<see cref="StockQuoteConfig"/>).</summary>
public sealed class StockHistoryConfig : StockQuoteConfig
{
    /// <summary>[S-1310] Mark the period's high and low on the chart.</summary>
    public bool ShowMinMax { get; set; }

    public override WidgetConfig Clone() => CopyQuoteTo(new StockHistoryConfig { ShowMinMax = ShowMinMax });
}
