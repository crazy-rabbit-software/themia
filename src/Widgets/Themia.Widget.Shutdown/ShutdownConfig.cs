using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-1910] Shut down.</summary>
public sealed class ShutdownConfig : PowerButtonConfig
{
    /// <summary>The action it stands for.</summary>
    [JsonIgnore]
    public override PowerAction Action => PowerAction.Shutdown;

    public override WidgetConfig Clone() => new ShutdownConfig { Style = Style };
}
