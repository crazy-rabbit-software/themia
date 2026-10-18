using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-3900] Wi-Fi.</summary>
public sealed class WifiToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.Wifi;

    public override WidgetConfig Clone() => new WifiToggleConfig { Style = Style };
}
