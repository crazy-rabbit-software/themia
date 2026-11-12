using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Widgets.Meters;

/// <summary>[S-430] One graphics adapter's load, the reading alone: the number at 28, or in a gauge ([S-6320]); the adapter's watch is the SDK's <see cref="GpuPart"/>.</summary>
internal sealed class GpuMeter : GpuPart
{
    public override WidgetId Kind => new WidgetId("gpu");

    /// <inheritdoc />
    public override bool Sized => true;

    public override MeterLook Look(WidgetConfig config) => Look((config as GpuConfig)?.Style ?? GpuStyle.Number);

    /// <summary>[S-6320] The drawing a style is.</summary>
    internal static MeterLook Look(GpuStyle style) => style == GpuStyle.Gauge ? MeterLook.Gauge : MeterLook.Number;
}
