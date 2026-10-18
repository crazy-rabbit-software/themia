using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-3900] Energy saver.</summary>
public sealed class EnergySaverToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.EnergySaver;

    public override WidgetConfig Clone() => new EnergySaverToggleConfig { Style = Style };
}
