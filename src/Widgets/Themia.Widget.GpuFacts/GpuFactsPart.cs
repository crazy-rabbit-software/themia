using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.GpuFacts;

namespace Themia.App.Widgets.Details;

/// <summary>
/// [S-2430]..[S-2432] One adapter's video memory, load and sensors, from the GPU meter's own hub feed
/// (<see cref="GpuSampler"/>: one query, one kernel handle per adapter, sampled on the pool once per
/// due tick for every GPU widget). The header names the adapter.
/// </summary>
internal sealed class GpuFactsPart : DetailPart
{
    /// <inheritdoc />
    public override CardOpen? Opens(WidgetConfig config) => GpuFactsModule.Opens;

    private readonly WatchSlot<GpuSnapshot> _gpu = new();
    private GpuFactsConfig _config = new();
    private IDetailHost? _host;

    public override WidgetId Kind => new WidgetId("gpuFacts");


    /// <summary>[S-11016] Fact rows alone, as the config picks them.</summary>
    public override DetailAtom Atom => DetailAtom.Facts;

    public override IReadOnlyList<FactOption> FactOptions => GpuDetailRules.Facts;

    public override void Watch(ISystemFeeds hub, WidgetConfig config, IDetailHost host)
    {
        _host = host;
        _config = config as GpuFactsConfig ?? new GpuFactsConfig();

        // Another adapter is the same snapshot, picked again: shown at once.
        _gpu.Hold(hub, Sources.Gpu, _ => Show());
        Show();
    }

    private void Show()
    {
        if (_host is not { } host || !_gpu.HasValue)
        {
            return;
        }

        var snapshot = _gpu.Value;

        if (snapshot.Details.Count == 0)
        {
            host.SetAbout(null);
            host.Present(GpuDetailRules.Absent(Strings.Current, GpuFactsStr.Details_Gpu_None.Text()));
            return;
        }

        // [S-2431] An adapter that is gone (a removed eGPU) shows the first one rather than nothing.
        var detail = snapshot.Details[_config.Adapter < snapshot.Details.Count ? _config.Adapter : 0];
        host.SetAbout(GpuRules.ShortName(detail.Name));
        host.Present(GpuDetailRules.Sheet(Strings.Current, detail));
    }

#if DEBUG
    /// <summary>[S-1942] No adapter, on any machine.</summary>
    public override DetailSheet? AbsentSheet() => GpuDetailRules.Absent(Strings.Current, GpuFactsStr.Details_Gpu_None.Text());
#endif

    public override void Detach()
    {
        _gpu.Release();
        _host = null;
    }
}
