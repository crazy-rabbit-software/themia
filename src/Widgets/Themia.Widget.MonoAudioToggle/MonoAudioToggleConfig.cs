using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-3900] Mono audio.</summary>
public sealed class MonoAudioToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.MonoAudio;

    public override WidgetConfig Clone() => new MonoAudioToggleConfig { Style = Style };
}
