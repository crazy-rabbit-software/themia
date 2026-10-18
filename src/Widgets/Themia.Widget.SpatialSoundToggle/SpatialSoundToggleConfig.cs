using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-3900] Spatial sound.</summary>
public sealed class SpatialSoundToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.SpatialSound;

    public override WidgetConfig Clone() => new SpatialSoundToggleConfig { Style = Style };
}
