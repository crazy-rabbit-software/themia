using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Widgets.Meters;

/// <summary>
/// [S-1190] [S-1191] Throughput across the active adapters: download as the reading, download and
/// upload as the chart's two series, both against the visible window's peak (the chart's
/// <see cref="ChartScale"/>, so the two share one top).
/// </summary>
internal sealed class NetworkMeter : MeterPart
{
    public override WidgetId Kind => new WidgetId("network");

    /// <summary>"1023 KB/s": the widest a rate is written.</summary>
    public override HeroBound Reading => HeroBound.Of(Formatting.Rate(1023 * 1024));

    /// <summary>[S-1124] Against the visible window's peak, never the session's; floor 1 KB/s.</summary>
    public override ChartScale Scale => ChartScale.Peak(Sources.NetworkChartFloor);

    /// <summary>[S-6270] Numbers: download and upload alone, both large.</summary>
    public override MeterLook Look(WidgetConfig config)
        => (config as NetworkConfig)?.Style == NetworkStyle.Numbers ? MeterLook.Numbers : MeterLook.Graph;

    /// <summary>2.4 MB/s down, 320 KB/s up.</summary>
    public override MeterReading SampleReading
        => new(0.6, Formatting.Rate(2.4 * 1024 * 1024), true, Value: 2.4 * 1024 * 1024, Second: 320 * 1024);
}
