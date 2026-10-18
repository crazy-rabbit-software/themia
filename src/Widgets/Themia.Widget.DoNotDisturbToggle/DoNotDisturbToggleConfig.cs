using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-3900] Do not disturb.</summary>
public sealed class DoNotDisturbToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.DoNotDisturb;

    public override WidgetConfig Clone() => new DoNotDisturbToggleConfig { Style = Style };
}
