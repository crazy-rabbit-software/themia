using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-3900] Color filters.</summary>
public sealed class ColorFiltersToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.ColorFilters;

    public override WidgetConfig Clone() => new ColorFiltersToggleConfig { Style = Style };
}
