using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-3900] Rotation lock.</summary>
public sealed class RotationLockToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.RotationLock;

    public override WidgetConfig Clone() => new RotationLockToggleConfig { Style = Style };
}
