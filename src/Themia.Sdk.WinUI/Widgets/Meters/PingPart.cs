using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets.Meters;

/// <summary>
/// [S-1170]..[S-1174] [S-11000] What every ping atom's meter shares (the reading, the history):
/// round-trip time to a host, the header's problem glyph while it does not answer (Offline on hover),
/// the host as the header's label, a chart scaled to the window's slowest answer with unanswered probes
/// on its baseline, and a Refresh command.
/// <para>
/// [S-2508] The probes are the hub's prober for this host, shared with every other ping atom and ping
/// statistics widget aimed at the same host: one probe out at a time, and a late answer from a host the
/// user has since changed belongs to the old host's prober, never this chart.
/// </para>
/// </summary>
public abstract class PingPart : MeterPart
{
    // [S-1170] [S-6710] The header's problem glyph: the caution warning while the host does not answer,
    // nothing while it does or while the first probe is out (no success green, [S-6706]).
    private readonly FontIcon _problem = HeaderBlock.Problem(string.Empty);
    private readonly HeaderParts _trailing = new();

    protected PingPart()
    {
        _trailing.Add(HeaderPartKind.Problem, _problem);
        _trailing.Want(_problem, false);
    }

    /// <summary>[S-2021] A click on the card opens Windows' network status page.</summary>
    public override CardOpen? Opens(WidgetConfig config) => CardOpen.Settings("network-status");

    /// <summary>"999 ms".</summary>
    public override ReadoutText Reading => Worst;

    /// <summary>"999 ms", the sizing's widest latency too.</summary>
    public static ReadoutText Worst => ReadoutRules.Split(Formatting.Latency(999));

    /// <summary>[S-1172] Against the slowest answer on screen, as the original scaled it (floor 1 ms).</summary>
    public override ChartScale Scale => ChartScale.Peak(1);

    /// <summary>[S-1170] Until the first probe is back the tile says so on hover, and the dot is quiet.</summary>
    public override MeterReading Waiting => MeterReading.Unavailable(PingStats.StatusText(PingStatus.Checking));

    public override MeterPressure PressureOf(MeterReading reading) => PingStats.Pressure(StatusOf(reading));

    public override FrameworkElement? HeaderTrailing => _trailing;

    public override void ApplyConfig(WidgetConfig config, IMeterHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        // [S-1173] The header's label names the host as the user does ([S-2260]).
        host.SetAbout(config is IPingHost ping ? PingHosts.Effective(ping) : HostAddress.DefaultPingHost);
    }

    /// <summary>[S-1170] The problem glyph while the host does not answer, its status on hover; nothing otherwise.</summary>
    public override void Observe(MeterReading reading, IMeterHost host)
    {
        var status = StatusOf(reading);
        _trailing.Want(_problem, status == PingStatus.Offline);
        ToolTipService.SetToolTip(_problem, status == PingStatus.Offline ? PingStats.StatusText(status) : null);
    }

    /// <summary>[S-1174] Refresh: probe now — unless one is already out, which is the answer coming.</summary>
    public override IReadOnlyList<WidgetAction> ContextActions(IMeterHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return [new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), host.SampleNow)];
    }

    /// <summary>
    /// [S-1170] Online when the last probe was answered; Offline when it was not (an unanswered probe
    /// still carries its baseline fraction, [S-1172]); Checking before the first one is back.
    /// </summary>
    private static PingStatus StatusOf(MeterReading reading)
        => reading.Available ? PingStatus.Online
            : reading.Fraction is not null ? PingStatus.Offline
            : PingStatus.Checking;
}
