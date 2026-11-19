using Themia.Core.Model;
using Themia.Sdk.Kit;

namespace Themia.Widget.WindGraph;

/// <summary>[S-10500] The graph's config: the place and window every weather graph holds ([S-12707]) and its chart look ([S-6752]: the line by default).</summary>
public sealed class WindGraphConfig : HourGraphConfig
{
    public WindGraphConfig()
        : base(ChartType.Line)
    {
    }

    /// <summary>[S-10502] Whether the gusts as the graph's second, tertiary line is drawn.</summary>
    public bool ShowGusts { get; set; } = true;

    public override WidgetConfig Clone()
    {
        var copy = new WindGraphConfig { ShowGusts = ShowGusts };
        CopyTo(copy);
        return copy;
    }
}
