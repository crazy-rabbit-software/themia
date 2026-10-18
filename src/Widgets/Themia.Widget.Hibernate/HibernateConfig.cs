using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-1910] Hibernate.</summary>
public sealed class HibernateConfig : PowerButtonConfig
{
    /// <summary>The action it stands for.</summary>
    [JsonIgnore]
    public override PowerAction Action => PowerAction.Hibernate;

    public override WidgetConfig Clone() => new HibernateConfig { Style = Style };
}
