using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-1910] Sign out.</summary>
public sealed class SignOutConfig : PowerButtonConfig
{
    /// <summary>The action it stands for.</summary>
    [JsonIgnore]
    public override PowerAction Action => PowerAction.SignOut;

    public override WidgetConfig Clone() => new SignOutConfig { Style = Style };
}
