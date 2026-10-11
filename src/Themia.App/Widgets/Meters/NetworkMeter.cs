using Themia.App.Services.SystemData;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets.Meters;

/// <summary>
/// [S-1190] [S-1191] Throughput across the active adapters: download as the reading, download and
/// upload as the chart's two series, both against the visible window's peak (the chart's
/// <see cref="ChartScale"/>, so the two share one top).
/// </summary>
internal sealed class NetworkMeter : MeterPart
{
    public override WidgetKind Kind => WidgetKind.Network;

    /// <summary>"1.2 MB/s".</summary>
    public override double ReadingCharacters => 8;

    /// <summary>[S-1124] Against the visible window's peak, never the session's; floor 1 KB/s.</summary>
    public override ChartScale Scale => ChartScale.Peak(Sources.NetworkChartFloor);

    public override MeterSeries Series(WidgetConfig config) => MeterSeries.Of(Sources.Network, Interval(config));
}
