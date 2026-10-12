using System.Text.Json.Serialization;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>
/// [S-430] The graphics meter: load of the busiest engine and its history, per adapter ([S-1762]).
/// A gauge, so it carries the history setting the other meters do, and it draws the same sparkline.
/// </summary>
public sealed class GpuConfig : GaugeConfig
{
    /// <summary>[S-436] A minute of history.</summary>
    public GpuConfig()
        : base(graphSpanSeconds: 60)
    {
    }

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Gpu;

    /// <summary>
    /// [S-433] Which adapters to show, as positions in the system's adapter list, in the order they
    /// are drawn. Untrusted on read: <see cref="GpuRules.ResolveAdapters"/> is the boundary.
    /// </summary>
    public List<int> Adapters { get; set; } = [0];

    public override WidgetConfig Clone()
    {
        var copy = new GpuConfig
        {
            // A new list: two widgets sharing one would follow each other's picker.
            Adapters = [.. Adapters ?? []],
        };

        CopyTo(copy);
        return copy;
    }
}
