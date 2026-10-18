using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-3900] Airplane mode.</summary>
public sealed class AirplaneModeToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.AirplaneMode;

    public override WidgetConfig Clone() => new AirplaneModeToggleConfig { Style = Style };
}
