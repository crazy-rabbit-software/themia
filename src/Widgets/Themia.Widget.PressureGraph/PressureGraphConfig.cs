using Themia.Core.Model;
using Themia.Sdk.Kit;

namespace Themia.Widget.PressureGraph;

/// <summary>[S-10700] The graph's config: the place and window every weather graph holds ([S-12707]) and its chart look ([S-6752]: the line by default).</summary>
public sealed class PressureGraphConfig : HourGraphConfig
{
    public PressureGraphConfig()
        : base(ChartType.Line)
    {
    }

    public override WidgetConfig Clone()
    {
        var copy = new PressureGraphConfig();
        CopyTo(copy);
        return copy;
    }
}
