using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets.Details;

/// <summary>
/// [S-2430]..[S-2432] One adapter's video memory, load and sensors, from the GPU meter's own shared
/// sampler (<see cref="GpuSampler"/>: one query, one kernel handle per adapter, sampled on the pool
/// for every GPU widget at once). The header names the adapter.
/// </summary>
internal sealed class GpuDetailPart : DetailPart
{
    private GpuDetailsConfig _config = new();
    private bool _held;
    private bool _inFlight;
    private bool _detached;

    public override WidgetKind Kind => WidgetKind.GpuDetails;

    public override double HeroCharacters => GpuDetailRules.HeroCharacters;

    public override bool ApplyConfig(WidgetConfig config, IDetailHost host)
    {
        var next = config as GpuDetailsConfig ?? new GpuDetailsConfig();
        var changed = next.Adapter != _config.Adapter;
        _config = next;
        return changed;
    }

    public override void Attach()
    {
        _detached = false;

        if (!_held)
        {
            GpuSampler.Acquire();
            _held = true;
        }
    }

    public override void Sample(IDetailHost host)
    {
        if (_inFlight || _detached || !_held)
        {
            return;
        }

        _inFlight = true;
        _ = SampleAsync(host);
    }

    private async Task SampleAsync(IDetailHost host)
    {
        try
        {
            var snapshot = await GpuSampler.SampleAsync().ConfigureAwait(true);

            if (_detached)
            {
                return;
            }

            if (snapshot.Details.Count == 0)
            {
                host.SetTitle(null);
                host.Present(DetailSheet.Missing(Str.Details_Gpu_None.Text()));
                return;
            }

            // [S-2431] An adapter that is gone (a removed eGPU) shows the first one rather than nothing.
            var detail = snapshot.Details[_config.Adapter < snapshot.Details.Count ? _config.Adapter : 0];
            host.SetTitle(GpuRules.ShortName(detail.Name));
            host.Present(GpuDetailRules.Sheet(Strings.Current, detail));
        }
        catch (Exception ex)
        {
            // Fire-and-forget from a tick: an escape here would go unobserved.
            Log.Error(Scope, "could not show the GPU details", ex);
        }
        finally
        {
            _inFlight = false;
        }
    }

    public override void Detach()
    {
        _detached = true;

        if (_held)
        {
            _held = false;
            GpuSampler.Release();
        }
    }
}
