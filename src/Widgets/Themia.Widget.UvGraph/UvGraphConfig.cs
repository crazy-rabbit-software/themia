using Themia.Core.Model;
using Themia.Sdk.Kit;

namespace Themia.Widget.UvGraph;

/// <summary>[S-10300] The graph's config: the place and window every weather graph holds ([S-12707]) and its chart look ([S-6752]: the bars by default).</summary>
public sealed class UvGraphConfig : HourGraphConfig
{
    public UvGraphConfig()
        : base(ChartType.Bars)
    {
    }

    public override WidgetConfig Clone()
    {
        var copy = new UvGraphConfig();
        CopyTo(copy);
        return copy;
    }
}
