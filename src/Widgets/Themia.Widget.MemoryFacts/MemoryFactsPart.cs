using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.MemoryFacts;

namespace Themia.App.Widgets.Details;

/// <summary>[S-2408] Memory in use, available, committed, cached, the pools and the page file.</summary>
internal sealed class MemoryFactsPart : DetailPart
{
    /// <inheritdoc />
    public override CardOpen? Opens(WidgetConfig config) => MemoryFactsModule.Opens;

    private readonly WatchSlot<PerformanceSnapshot> _figures = new();
    private IDetailHost? _host;

    public override WidgetId Kind => new WidgetId("memoryFacts");


    /// <summary>[S-11017] Fact rows alone, as the config picks them.</summary>
    public override DetailAtom Atom => DetailAtom.Facts;

    public override IReadOnlyList<FactOption> FactOptions => MemoryDetailRules.Facts;

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

    public override void Detach()
    {
        _figures.Release();
        _host = null;
    }
}
