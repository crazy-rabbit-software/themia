using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets.Details;

/// <summary>
/// [S-2426]..[S-2429] Latency statistics over the latest probes of one host: the newest answer,
/// average, range, jitter and loss. Probes go through the ping meter's own
/// <see cref="NetworkMetrics.PingAsync"/>: one at a time, never stacked, and a late answer from a
/// host the user has since changed is dropped ([S-106], [S-107]).
/// </summary>
internal sealed class PingDetailPart : DetailPart
{
    private readonly PingWindow _window = new(60);

    private PingDetailsConfig _config = new();
    private CancellationTokenSource? _probe;
    private string? _host;
    private string? _detail;
    private bool _anyProbe;
    private bool _detached;

    public override WidgetKind Kind => WidgetKind.PingDetails;

    public override double HeroCharacters => PingDetailRules.HeroCharacters;

    public override bool ApplyConfig(WidgetConfig config, IDetailHost host)
    {
        _detached = false;
        _config = config as PingDetailsConfig ?? new PingDetailsConfig();
        _window.Capacity = _config.EffectiveWindow;

        // [S-1173] The header names the host as the user does.
        host.SetTitle(_config.EffectiveLabel ?? _config.EffectiveHost);

        var previous = _host;
        _host = _config.EffectiveHost;

        if (_host == previous)
        {
            return false;
        }

        // A different host is a different measurement: what the window holds belongs to the old one.
        CancelProbe();
        _window.Clear();
        _anyProbe = false;
        return previous is not null;
    }

    public override IReadOnlyList<WidgetAction> ContextActions(IDetailHost host)
        => [new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), host.SampleNow)];

    public override void Sample(IDetailHost host)
    {
        if (_probe is not null || _detached)
        {
            return;
        }

        if (!_anyProbe)
        {
            Show(host);
        }

        var probe = new CancellationTokenSource();
        _probe = probe;
        _ = ProbeAsync(_host ?? PingConfig.DefaultHost, probe, host);
    }

    private async Task ProbeAsync(string target, CancellationTokenSource probe, IDetailHost host)
    {
        try
        {
            var outcome = await NetworkMetrics.PingAsync(target, probe.Token).ConfigureAwait(true);

            if (_detached || !ReferenceEquals(_probe, probe))
            {
                return;
            }

            Note(outcome.Detail);
            _anyProbe = true;
            _window.Add(outcome.Reading is { Available: true, Value: { } ms } ? ms : null);
            Show(host);
        }
        catch (OperationCanceledException)
        {
            // ignore: cancelled by Detach or a host change; there is nobody left to show it to
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"could not probe {target}", ex);
        }
        finally
        {
            if (ReferenceEquals(_probe, probe))
            {
                _probe = null;
            }

            probe.Dispose();
        }
    }

    private void Show(IDetailHost host)
    {
        var stats = _window.Stats();
        var status = PingStats.Status(_anyProbe, stats.Last is not null);
        host.Present(PingDetailRules.Sheet(Strings.Current, stats, _config.EffectiveHost, status));
    }

    /// <summary>A probe is logged only when it says something the last one did not.</summary>
    private void Note(string? detail)
    {
        if (detail == _detail)
        {
            return;
        }

        _detail = detail;

        if (detail is null)
        {
            Log.Info(Scope, $"ping answering again: {_host}");
        }
        else
        {
            Log.Warn(Scope, detail);
        }
    }

    private void CancelProbe()
    {
        var probe = _probe;
        _probe = null;

        // Cancel only: ProbeAsync's finally owns the dispose.
        probe?.Cancel();
    }

    public override void Detach()
    {
        _detached = true;
        CancelProbe();
    }
}
