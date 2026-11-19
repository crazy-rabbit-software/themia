using System.Text.Json.Serialization;
using Themia.Core.Model;
using Themia.Sdk.Kit;

namespace Themia.Widget.ExchangeHistory;

/// <summary>
/// [S-11155] One pair's last 90 days as a chart: the pair is the rate atom's (<see cref="ExchangePairConfig"/>).
/// [S-6752] Its chart look: the line with its wash (its look until now), the line alone or candles; Simple or Detailed.
/// </summary>
public sealed class ExchangeHistoryConfig : ExchangePairConfig, IChartLooks
{
    public ChartType Chart { get; set; } = ChartType.Area;

    public ChartDetail Detail { get; set; }

    [JsonIgnore]
    public IReadOnlyList<ChartType> ChartTypes => ChartLooks.Prices;

    [JsonIgnore]
    public ChartType DefaultChart => ChartType.Area;

    [JsonIgnore]
    public bool OffersDetail => true;

    public override IReadOnlyList<string> Repair() => [.. base.Repair(), .. ChartLooks.Repair(this, "exchangeHistory")];

    public override WidgetConfig Clone()
    {
        var copy = CopyPairTo(new ExchangeHistoryConfig());
        ChartLooks.Copy(this, copy);
        return copy;
    }
}
