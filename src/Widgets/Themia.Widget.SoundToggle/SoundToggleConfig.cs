using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-3900] Sound (the default output's mute).</summary>
public sealed class SoundToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.Sound;

    public override WidgetConfig Clone() => new SoundToggleConfig { Style = Style };
}
