using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-1910] Lock.</summary>
public sealed class LockConfig : PowerButtonConfig
{
    /// <summary>The action it stands for.</summary>
    [JsonIgnore]
    public override PowerAction Action => PowerAction.Lock;

    public override WidgetConfig Clone() => new LockConfig { Style = Style };
}
