using Themia.App.Services;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Widget.Network;

namespace Themia.App.Widgets.Meters;

/// <summary>[S-1190] [S-1191] Throughput across the active adapters, the reading alone: download over upload, both at 28.</summary>
internal sealed class NetworkMeter : MeterPart
{
    /// <inheritdoc />
    public override CardOpen? Opens(WidgetConfig config) => NetworkModule.Opens;

    public override WidgetId Kind => new WidgetId("network");

    /// <summary>"1023 KB/s": the widest a rate is written.</summary>
    public override ReadoutText Reading => Worst;

    /// <summary>"1023 KB/s", the sizing's widest rate too.</summary>
    internal static ReadoutText Worst => ReadoutRules.Split(Formatting.Rate(1023 * 1024));

    /// <summary>[S-6264] Download over upload, each led by its arrow.</summary>
    public override MeterLook Look(WidgetConfig config) => MeterLook.Numbers;
}
