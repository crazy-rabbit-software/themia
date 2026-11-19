using Themia.Core.Model;
using Themia.Sdk.Kit;

namespace Themia.Widget.HumidityGraph;

/// <summary>[S-10600] The graph's config: the place and window every weather graph holds ([S-12707]) and its chart look ([S-6752]: the area by default).</summary>
public sealed class HumidityGraphConfig : HourGraphConfig
{
    public HumidityGraphConfig()
        : base(ChartType.Area)
    {
    }

    public override WidgetConfig Clone()
    {
        var copy = new HumidityGraphConfig();
        CopyTo(copy);
        return copy;
    }
}
