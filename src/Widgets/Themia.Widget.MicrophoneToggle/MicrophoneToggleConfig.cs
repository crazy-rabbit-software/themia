using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-3900] Microphone (the default microphone's mute).</summary>
public sealed class MicrophoneToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.Microphone;

    public override WidgetConfig Clone() => new MicrophoneToggleConfig { Style = Style };
}
