using Themia.Core.Model;
using Themia.Sdk.Kit;

namespace Themia.Widget.TemperatureGraph;

/// <summary>
/// [S-10100] A temperature graph: a place, its window, its chart look ([S-6752]: the curve with its wash by default)
/// and whether the feels-like line is drawn.
/// </summary>
public sealed class TemperatureGraphConfig : HourGraphConfig
{
    public TemperatureGraphConfig()
        : base(ChartType.Area)
    {
    }

    /// <summary>[S-10102] The apparent temperature as the graph's second, tertiary line.</summary>
    public bool ShowFeelsLike { get; set; } = true;

    public override WidgetConfig Clone()
    {
        var copy = new TemperatureGraphConfig { ShowFeelsLike = ShowFeelsLike };
        CopyTo(copy);
        return copy;
    }
}
