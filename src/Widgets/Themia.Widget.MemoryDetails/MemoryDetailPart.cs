using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.MemoryDetails;

namespace Themia.App.Widgets.Details;

/// <summary>[S-2408] Memory in use, available, committed, cached, the pools and the page file.</summary>
internal sealed class MemoryDetailPart : DetailPart
{
    /// <inheritdoc />
    public override CardOpen? Opens(WidgetConfig config) => MemoryDetailsModule.Opens;

    private readonly WatchSlot<PerformanceSnapshot> _figures = new();
    private IDetailHost? _host;

    public override WidgetId Kind => new WidgetId("memoryDetails");

    public override string HeroWorst => MemoryDetailRules.HeroWorst;

    public override void Watch(ISystemFeeds hub, WidgetConfig config, IDetailHost host)
    {
        _host = host;
        _figures.Hold(hub, Sources.Performance, _ => Show());
        Show();
    }

    private void Show()
    {
        if (_host is { } host && _figures.HasValue)
        {
            host.Present(MemoryDetailRules.Sheet(Strings.Current, _figures.Value.Figures, _figures.Value.Pagefile));
        }
    }

#if DEBUG
    /// <summary>[S-1917] A fixed 64 GB machine, so the renders are one picture on every machine.</summary>
    public override DetailSheet? SampleSheet()
    {
        const ulong Gb = 1UL << 30;
        var figures = new PerformanceFigures(98 * Gb, 114 * Gb, 64 * Gb, 29 * Gb, 24 * Gb, 41 * Gb / 10, 29 * Gb / 10, 209_439, 382, 7393);
        return MemoryDetailRules.Sheet(Strings.Current, figures, new PagefileUsage(50 * Gb, 17 * Gb / 10));
    }
#endif

    public override void Detach()
    {
        _figures.Release();
        _host = null;
    }
}
