using Themia.Core.Links;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Widgets.Meters;

/// <summary>[S-430] One graphics adapter's load, the reading alone: the number at 28, or in a gauge ([S-6320]); the adapter's watch is the SDK's <see cref="GpuPart"/>.</summary>
internal sealed class GpuMeter : GpuPart
{
    /// <summary>[S-11912] At a moment pointed at on a history of its subject, the reading shows its value then.</summary>
    public override LinkTopic<InstantLink>? Instant => LinkTopics.HoveredInstant;

    public override WidgetId Kind => new WidgetId("gpu");

    public override MeterLook Look(WidgetConfig config) => Look((config as GpuConfig)?.Style ?? GpuStyle.Number);

    /// <summary>[S-6320] The drawing a style is.</summary>
    internal static MeterLook Look(GpuStyle style) => style == GpuStyle.Gauge ? MeterLook.Gauge : MeterLook.Number;
}
