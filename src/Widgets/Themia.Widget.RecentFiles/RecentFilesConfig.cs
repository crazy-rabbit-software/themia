using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-4840] Recent files. Nothing to set: the list is Windows' own Recent Items.</summary>
public sealed class RecentFilesConfig : WidgetConfig
{
    /// <summary>[S-4843] Keeps "2h" honest; the list itself redraws from the folder watch.</summary>
    [JsonIgnore]
    public TimeSpan TickInterval => TimeSpan.FromMinutes(1);

    public override WidgetConfig Clone() => new RecentFilesConfig();
}
