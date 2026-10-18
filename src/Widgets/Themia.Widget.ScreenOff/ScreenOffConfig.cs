using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-1910] Turn off the display.</summary>
public sealed class ScreenOffConfig : PowerButtonConfig
{
    /// <summary>The action it stands for.</summary>
    [JsonIgnore]
    public override PowerAction Action => PowerAction.ScreenOff;

    public override WidgetConfig Clone() => new ScreenOffConfig { Style = Style };
}
