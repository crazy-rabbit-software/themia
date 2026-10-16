using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets.Meters;

/// <summary>
/// [S-1170]..[S-1174] Round-trip time to a host, with the original's status as a dot in the header
/// (Online / Offline / Checking… on hover), a chart scaled to the window's slowest answer with
/// unanswered probes on its baseline, and a Refresh command.
/// <para>
/// [S-2508] The probes are the hub's prober for this host, shared with every other ping
/// meter and ping statistics widget aimed at the same host: one probe out at a time, and a late
/// answer from a host the user has since changed belongs to the old host's prober, never this chart.
/// </para>
/// </summary>
internal sealed class PingMeter : MeterPart
{
    private static readonly Brush OnlineBrush = FluentLayout.Themed("SystemFillColorSuccessBrush", Colors.Green);
    private static readonly Brush OfflineBrush = FluentLayout.Themed("SystemFillColorCriticalBrush", Colors.Red);
    private static readonly Brush CheckingBrush = FluentLayout.Themed("TextFillColorTertiaryBrush", Colors.Gray);

    private readonly Ellipse _dot = new()
    {
        Width = Fluent.Space2,
        Height = Fluent.Space2,
        VerticalAlignment = VerticalAlignment.Center,
        Fill = CheckingBrush,
    };

    public override WidgetKind Kind => WidgetKind.Ping;

    /// <summary>"999 ms".</summary>
    public override HeroBound Reading => HeroBound.Of(Formatting.Latency(999));

    /// <summary>[S-1172] Against the slowest answer on screen, as the original scaled it (floor 1 ms).</summary>
    public override ChartScale Scale => ChartScale.Peak(1);

    /// <summary>[S-1170] Until the first probe is back the tile says so on hover, and the dot is quiet.</summary>
    public override MeterReading Waiting => MeterReading.Unavailable(PingStats.StatusText(PingStatus.Checking));

    public override MeterPressure PressureOf(MeterReading reading) => PingStats.Pressure(StatusOf(reading));

    public override FrameworkElement? HeaderTrailing => _dot;

    public override void ApplyConfig(WidgetConfig config, IMeterHost host)
    {
        // [S-1173] The header names the host as the user does: its label, else its address.
        var ping = config as PingConfig ?? new PingConfig();
        host.SetTitle(ping.EffectiveHost);
    }

    /// <summary>[S-1170] The header dot: success, critical, or quiet while checking.</summary>
    public override void Observe(MeterReading reading, IMeterHost host)
    {
        var status = StatusOf(reading);
        _dot.Fill = status switch
        {
            PingStatus.Online => OnlineBrush,
            PingStatus.Offline => OfflineBrush,
            _ => CheckingBrush,
        };

        ToolTipService.SetToolTip(_dot, PingStats.StatusText(status));
    }

    /// <summary>[S-1174] Refresh: probe now — unless one is already out, which is the answer coming.</summary>
    public override IReadOnlyList<WidgetAction> ContextActions(IMeterHost host)
        => [new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), host.SampleNow)];

    /// <summary>
    /// [S-1170] Online when the last probe was answered; Offline when it was not (an unanswered probe
    /// still carries its baseline fraction, [S-1172]); Checking before the first one is back.
    /// </summary>
    private static PingStatus StatusOf(MeterReading reading)
        => reading.Available ? PingStatus.Online
            : reading.Fraction is not null ? PingStatus.Offline
            : PingStatus.Checking;
}
