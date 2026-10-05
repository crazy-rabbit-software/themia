using Themia.App.Services;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets.Meters;

/// <summary>
/// [S-1190] [S-1191] Throughput across the active adapters: download as the reading, download and
/// upload as the chart's two series, both against the visible window's peak.
/// </summary>
internal sealed class NetworkMeter : MeterPart
{
    /// <summary>The lowest top the chart may have: 1 KB/s, so idle chatter stays a flat line (the original's floor).</summary>
    private const double ChartFloor = 1024;

    private readonly RateWindow _window = new(60);

    public override WidgetKind Kind => WidgetKind.Network;

    /// <summary>"1.2 MB/s".</summary>
    public override double ReadingCharacters => 8;

    /// <summary>[S-1124] Against the visible window's peak, never the session's.</summary>
    public override ChartScale Scale => ChartScale.Peak(ChartFloor);

    public override bool ApplyConfig(WidgetConfig config, IMeterHost host)
    {
        // [S-1191] The fraction's window is the chart's: the same span, so the two agree on the top.
        _window.Capacity = (config as NetworkConfig ?? new NetworkConfig()).GraphCapacity;
        return false;
    }

    public override void Sample(IMeterHost host)
    {
        if (NetworkThroughput.Read() is not { } sample)
        {
            host.Present(MeterReading.Unavailable(Str.WidgetsB_Meters_Unavailable.Text()));
            return;
        }

        if (sample.Interfaces.Count == 0)
        {
            _window.Clear();
            host.Present(MeterReading.Unavailable(Str.WidgetsB_Network_NoConnection.Text()));
            return;
        }

        Present(host, sample.Down, sample.Up);
    }

    private void Present(IMeterHost host, double down, double up)
    {
        _window.Push(down, up);

        host.Present(new MeterReading(
            _window.Fraction(down, ChartFloor),
            Formatting.Rate(down),
            true,
            Value: down,
            Second: up));
    }

}
