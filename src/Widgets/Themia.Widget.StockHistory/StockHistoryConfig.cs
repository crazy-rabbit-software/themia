using System.Text.Json.Serialization;
using Themia.Core.Model;
using Themia.Sdk.Kit;

namespace Themia.Widget.StockHistory;

/// <summary>
/// [S-11165] One instrument's period as a chart: the instrument and its period are the price atom's (<see cref="StockQuoteConfig"/>).
/// [S-6752] Its chart look: the line with its wash (its look until now), the line alone or candles; Simple or Detailed.
/// </summary>
public sealed class StockHistoryConfig : StockQuoteConfig, IChartLooks
{
    /// <summary>[S-1310] Mark the period's high and low on the chart.</summary>
    public bool ShowMinMax { get; set; }

    public ChartType Chart { get; set; } = ChartType.Area;

    public ChartDetail Detail { get; set; }

    [JsonIgnore]
    public IReadOnlyList<ChartType> ChartTypes => ChartLooks.Prices;

    [JsonIgnore]
    public ChartType DefaultChart => ChartType.Area;

    [JsonIgnore]
    public bool OffersDetail => true;

    public override IReadOnlyList<string> Repair() => [.. base.Repair(), .. ChartLooks.Repair(this, "stockHistory")];

    public override WidgetConfig Clone()
    {
        var copy = CopyQuoteTo(new StockHistoryConfig { ShowMinMax = ShowMinMax });
        ChartLooks.Copy(this, copy);
        return copy;
    }
}
