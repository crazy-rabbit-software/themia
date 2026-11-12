using Themia.Core.Links;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Widgets.Meters;

/// <summary>[S-1170] Round-trip time to a host, the reading alone: the latency at 28, or over signal bars ([S-6273]); the host's prober and header are the SDK's <see cref="PingPart"/>.</summary>
internal sealed class PingMeter : PingPart
{
    /// <summary>[S-11912] At a moment pointed at on a history of its subject, the reading shows its value then.</summary>
    public override LinkTopic<InstantLink>? Instant => LinkTopics.HoveredInstant;

    public override WidgetId Kind => new WidgetId("ping");

    public override MeterLook Look(WidgetConfig config) => Look((config as PingConfig)?.Style ?? PingStyle.Number);

    /// <summary>[S-6273] The drawing a style is.</summary>
    internal static MeterLook Look(PingStyle style) => style == PingStyle.Signal ? MeterLook.Signal : MeterLook.Number;
}
