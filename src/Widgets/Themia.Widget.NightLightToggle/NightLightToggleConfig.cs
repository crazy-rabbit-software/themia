using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-3900] Night light.</summary>
public sealed class NightLightToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.NightLight;

    public override WidgetConfig Clone() => new NightLightToggleConfig { Style = Style };
}
