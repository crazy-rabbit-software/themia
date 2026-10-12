using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-1180] Charge level: the reading with the battery icon, and its history ([S-1762]).</summary>
public sealed class BatteryConfig : GaugeConfig
{
    /// <summary>Charge moves slowly: a quarter of an hour of history.</summary>
    public BatteryConfig()
        : base(graphSpanSeconds: 900)
    {
    }

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Battery;

    public override WidgetConfig Clone()
    {
        var copy = new BatteryConfig();
        CopyTo(copy);
        return copy;
    }
}
