using Themia.App.Services;
using Themia.Core.Links;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets.Meters;

/// <summary>[S-1820] Disk activity, the reading alone: the rate at 28, or over reads and writes as two bars ([S-6271]); the drive's watch is the SDK's <see cref="DiskPart"/>.</summary>
internal sealed class DiskMeter(IWidgetHost host) : DiskPart(host)
{
    /// <summary>[S-11912] At a moment pointed at on a history of its subject, the reading shows its value then.</summary>
    public override LinkTopic<InstantLink>? Instant => LinkTopics.HoveredInstant;

    /// <summary>[S-11953] The card is its drive on the view's links: it publishes it and lights for it.</summary>
    public override LinkTopic<DriveLink>? Drive => LinkTopics.HoveredDrive;

    public override WidgetId Kind => new WidgetId("disk");

    public override MeterLook Look(WidgetConfig config) => Look((config as DiskConfig)?.Style ?? DiskStyle.Number);

    /// <summary>[S-6271] The drawing a style is: the bars are rows under the rate.</summary>
    internal static MeterLook Look(DiskStyle style) => style == DiskStyle.Bars ? MeterLook.Rows : MeterLook.Number;
}
