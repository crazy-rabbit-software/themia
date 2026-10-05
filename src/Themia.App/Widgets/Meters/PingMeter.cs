using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets.Meters;

/// <summary>
/// [S-1170]..[S-1174] Round-trip time to a host, with the original's status as a dot in the header
/// (Online / Offline / Checking… on hover), a chart scaled to the window's slowest answer with
/// unanswered probes on its baseline, and a Refresh command.
/// <para>
/// The one reading that cannot be taken synchronously: a probe takes up to two seconds and the tick
/// runs on the UI thread, so <see cref="Sample"/> starts a probe and the reading lands later.
/// </para>
/// </summary>
internal sealed class PingMeter : MeterPart
{
    private static readonly Brush OnlineBrush = FluentLayout.Themed("SystemFillColorSuccessBrush", Colors.Green);
    private static readonly Brush OfflineBrush = FluentLayout.Themed("SystemFillColorCriticalBrush", Colors.Red);
    private static readonly Brush CheckingBrush = FluentLayout.Themed("TextFillColorTertiaryBrush", Colors.Gray);

    // Every probe since the history started, answered or not; as long as the chart spans. The
    // status dot reads it.
    private readonly PingHistory _history = new(60);

    private readonly Ellipse _dot = new()
    {
        Width = Fluent.Space2,
        Height = Fluent.Space2,
        VerticalAlignment = VerticalAlignment.Center,
        Fill = CheckingBrush,
    };

    private PingConfig _config = new();

    // The token source is both the cancel handle and the "a probe is already out" flag — one piece
    // of state, so the two can never disagree.
    private CancellationTokenSource? _probe;
    private string? _host;
    private int _interval;
    private string? _detail;
    private bool _detached;

    public override WidgetKind Kind => WidgetKind.Ping;

    /// <summary>"999 ms".</summary>
    public override double ReadingCharacters => Formatting.Latency(999).Length;

    /// <summary>[S-1172] Against the slowest answer on screen, as the original scaled it (floor 1 ms).</summary>
    public override ChartScale Scale => ChartScale.Peak(1);

    public override MeterPressure PressureOf(MeterReading reading) => PingStats.Pressure(Status);

    public override FrameworkElement? HeaderTrailing => _dot;

    private PingStatus Status => PingStats.Status(_history.Any, _history.Last is not null);

    public override bool ApplyConfig(WidgetConfig config, IMeterHost host)
    {
        _detached = false;
        _config = config as PingConfig ?? new PingConfig();
        _history.Capacity = _config.GraphCapacity;

        // A new cadence restarts the view's chart ([S-1120]); the probes the dot reads start with it.
        if (_interval != 0 && _interval != _config.EffectiveIntervalSeconds)
        {
            _history.Clear();
        }

        _interval = _config.EffectiveIntervalSeconds;

        // [S-1173] The header names the host as the user does: its label, else its address.
        host.SetTitle(_config.EffectiveLabel ?? _config.EffectiveHost);

        var previous = _host;
        _host = _config.EffectiveHost;

        if (_host == previous)
        {
            return false;
        }

        // Before the view resubscribes, which fires a tick of its own and would start a probe on
        // the new host — cancelling afterwards would kill exactly that probe.
        CancelProbe();

        // A different host is a different measurement, so the samples already on the chart belong
        // to something else. On the first config there is nothing to clear.
        _history.Clear();
        ShowStatus();

        if (previous is null)
        {
            return false;
        }

        host.ClearHistory();
        return true;
    }

    /// <summary>[S-1174] Refresh: probe now — unless one is already out, which is the answer coming.</summary>
    public override IReadOnlyList<WidgetAction> ContextActions(IMeterHost host)
        => [new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), host.SampleNow)];

    /// <summary>
    /// Send one probe, unless the last one is still out.
    /// <para>
    /// A dead host takes the full two-second timeout to answer, which at a one-second cadence would
    /// mean two probes in flight, then three; the queue grows for as long as the host stays down and
    /// every reading the tile shows is older than the last tick. One at a time means a slow host
    /// simply updates more slowly, which is the truth.
    /// </para>
    /// </summary>
    public override void Sample(IMeterHost host)
    {
        if (_probe is not null || _detached)
        {
            return;
        }

        // [S-1170] Until the first probe is back the tile says so on hover, and the dot is quiet.
        if (!_history.Any)
        {
            host.Present(MeterReading.Unavailable(PingStats.StatusText(PingStatus.Checking)));
        }

        var probe = new CancellationTokenSource();
        _probe = probe;
        _ = ProbeAsync(_host ?? PingConfig.DefaultHost, probe, host);
    }

    private async Task ProbeAsync(string target, CancellationTokenSource probe, IMeterHost host)
    {
        try
        {
            var outcome = await NetworkMetrics.PingAsync(target, probe.Token).ConfigureAwait(true);

            // A late answer from a detached view, or from a probe aimed at a host the user has
            // since changed, is not a reading — it is a stale fact about something else.
            if (_detached || !ReferenceEquals(_probe, probe))
            {
                return;
            }

            NoteProbeState(outcome.Detail);
            Record(outcome.Reading is { Available: true, Value: { } ms } ? ms : null, host, outcome.Reading.Reason);
        }
        catch (OperationCanceledException)
        {
            // ignore: cancelled by Detach or by a host change, so there is nothing left to show to
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"could not probe {target}", ex);

            if (!_detached && ReferenceEquals(_probe, probe))
            {
                Record(null, host);
            }
        }
        finally
        {
            // The task that created the source owns it, on every path: cancelling elsewhere only
            // clears the field, so exactly one Dispose can ever run for one source.
            if (ReferenceEquals(_probe, probe))
            {
                _probe = null;
            }

            probe.Dispose();
        }
    }

    /// <summary>
    /// [S-1170] [S-1172] One probe into the history, and the reading it makes. An unanswered probe is
    /// a dimmed "--" (Offline on hover) that still plots — on the baseline, as the original drew it —
    /// so a dropout shows on the chart as a dip rather than as a gap that looks like nothing happened.
    /// </summary>
    private void Record(double? latency, IMeterHost host, string? reason = null)
    {
        _history.Add(latency);
        ShowStatus();

        host.Present(latency is { } ms
            ? new MeterReading(Math.Clamp(ms / NetworkMetrics.LatencyCeilingMs, 0, 1), Formatting.Latency(ms), true, Value: ms)
            : new MeterReading(0, "--", false, Value: 0, Reason: reason ?? PingStats.StatusText(Status)));
    }

    /// <summary>[S-1170] The header dot: success, critical, or quiet while checking.</summary>
    private void ShowStatus()
    {
        var status = Status;
        _dot.Fill = status switch
        {
            PingStatus.Online => OnlineBrush,
            PingStatus.Offline => OfflineBrush,
            _ => CheckingBrush,
        };

        ToolTipService.SetToolTip(_dot, PingStats.StatusText(status));
    }

    /// <summary>
    /// Log a probe only when it says something the previous one did not: an unreachable host
    /// answers identically on every tick, and one line per tick would bury the rest of the log.
    /// </summary>
    private void NoteProbeState(string? detail)
    {
        if (detail == _detail)
        {
            return;
        }

        _detail = detail;

        if (detail is null)
        {
            Log.Info(Scope, $"ping answering again: {_host}");
            return;
        }

        Log.Warn(Scope, detail);
    }

    private void CancelProbe()
    {
        var probe = _probe;
        _probe = null;

        // Cancel only. Disposal belongs to ProbeAsync's finally, which runs on the continuation
        // this cancel is about to release.
        probe?.Cancel();
    }

    public override void Detach()
    {
        _detached = true;
        CancelProbe();
    }
}
