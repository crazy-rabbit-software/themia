using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Widgets.Details;

/// <summary>[S-2408] Memory in use, available, committed, cached, the pools and the page file.</summary>
internal sealed class MemoryDetailPart : DetailPart
{
    private readonly WatchSlot<PerformanceSnapshot> _figures = new();
    private IDetailHost? _host;

    public override WidgetId Kind => MemoryDetailsConfig.Descriptor.Id;

    public override double HeroCharacters => MemoryDetailRules.HeroCharacters;

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
