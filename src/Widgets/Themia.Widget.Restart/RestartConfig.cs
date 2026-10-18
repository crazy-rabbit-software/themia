using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-1910] Restart.</summary>
public sealed class RestartConfig : PowerButtonConfig
{
    /// <summary>The action it stands for.</summary>
    [JsonIgnore]
    public override PowerAction Action => PowerAction.Restart;

    public override WidgetConfig Clone() => new RestartConfig { Style = Style };
}
