using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-1910] Sleep.</summary>
public sealed class SleepConfig : PowerButtonConfig
{
    /// <summary>The action it stands for.</summary>
    [JsonIgnore]
    public override PowerAction Action => PowerAction.Sleep;

    public override WidgetConfig Clone() => new SleepConfig { Style = Style };
}
