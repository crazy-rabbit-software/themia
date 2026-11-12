using Themia.App.Services;
using Themia.Core.Links;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Cpu;

namespace Themia.App.Widgets.Meters;

/// <summary>[S-1140] Processor load across all cores, the reading alone: the number at 28, or in a gauge ([S-6311]).</summary>
internal sealed class CpuMeter : MeterPart
{
    /// <summary>[S-11912] At a moment pointed at on a history of its subject, the reading shows its value then.</summary>
    public override LinkTopic<InstantLink>? Instant => LinkTopics.HoveredInstant;

    /// <inheritdoc />
    public override CardOpen? Opens(WidgetConfig config) => CpuModule.Opens;

    public override WidgetId Kind => new WidgetId("cpu");

    public override MeterPressure PressureOf(MeterReading reading)
        => MeterPressures.For(PressureScale.Usage, reading.Available ? reading.Fraction : null);

    public override MeterLook Look(WidgetConfig config) => Look((config as CpuConfig)?.Style ?? CpuStyle.Number);

    /// <summary>[S-6311] The drawing a style is.</summary>
    internal static MeterLook Look(CpuStyle style) => style == CpuStyle.Gauge ? MeterLook.Gauge : MeterLook.Number;
}
