using Themia.App.Services;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets.Details;

/// <summary>[S-2405] The processor's speed, cores, processes and uptime; the header names the processor.</summary>
internal sealed class CpuDetailPart : DetailPart
{
    private IDisposable? _lease;

    public override WidgetKind Kind => WidgetKind.CpuDetails;

    public override double HeroCharacters => CpuDetailRules.HeroCharacters;

    public override void Attach() => _lease ??= ProcessorInfo.Acquire();

    public override void Sample(IDetailHost host)
    {
        var figures = ProcessorInfo.Read();
        host.SetTitle(figures.Name);
        host.Present(CpuDetailRules.Sheet(Strings.Current, figures));
    }

    public override void Detach()
    {
        _lease?.Dispose();
        _lease = null;
    }
}

/// <summary>[S-2408] Memory in use, available, committed, cached, the pools and the page file.</summary>
internal sealed class MemoryDetailPart : DetailPart
{
    public override WidgetKind Kind => WidgetKind.MemoryDetails;

    public override double HeroCharacters => MemoryDetailRules.HeroCharacters;

    public override void Sample(IDetailHost host)
        => host.Present(MemoryDetailRules.Sheet(Strings.Current, PerformanceInfo.Read(), PerformanceInfo.Pagefile()));
}
