using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Widgets.Details;

/// <summary>
/// [S-2405] The processor's speed, cores, processes and uptime; the header names the processor. The
/// load is the CPU meter's own feed and the counts the memory details' one:
/// the hub reads both before the processor on a tick ([S-2501]), so the sheet is one tick's figures and
/// the CPU baseline is never moved by a second reader.
/// </summary>
internal sealed class CpuDetailPart : DetailPart
{
    private readonly WatchSlot<MeterReading> _load = new();
    private readonly WatchSlot<PerformanceSnapshot> _counts = new();
    private readonly WatchSlot<CpuFigures> _processor = new();
    private IDetailHost? _host;

    public override WidgetId Kind => new WidgetId("cpuDetails");

    public override double HeroCharacters => CpuDetailRules.HeroCharacters;

    public override void Watch(ISystemFeeds hub, WidgetConfig config, IDetailHost host)
    {
        _host = host;
        // The load and the counts only need to be read; the processor's read, last of the three on a
        // tick, is what presents the sheet.
        _load.Hold(hub, Sources.Cpu, static _ => { });
        _counts.Hold(hub, Sources.Performance, static _ => { });
        _processor.Hold(hub, Sources.Processor, _ => Show());
        Show();
    }

    private void Show()
    {
        if (_host is not { } host || !_processor.HasValue)
        {
            return;
        }

        var load = _load.HasValue && _load.Value.Available ? _load.Value.Fraction : null;
        var counts = _counts.HasValue ? _counts.Value.Figures : null;
        var figures = _processor.Value with
        {
            Load = load,
            Processes = counts?.Processes,
            Threads = counts?.Threads,
            Handles = counts?.Handles,
        };

        host.SetTitle(figures.Name);
        host.Present(CpuDetailRules.Sheet(Strings.Current, figures));
    }

    public override void Detach()
    {
        _processor.Release();
        _counts.Release();
        _load.Release();
        _host = null;
    }
}
