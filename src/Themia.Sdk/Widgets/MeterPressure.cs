namespace Themia.Core.Widgets;

/// <summary>[S-1123] What a meter's colour says about its reading.</summary>
public enum MeterPressure
{
    /// <summary>Nothing to note: the accent colour.</summary>
    Normal = 0,

    /// <summary>Getting full or getting low: the caution colour.</summary>
    Elevated = 1,

    /// <summary>Nearly full, or nearly empty: the critical colour.</summary>
    Critical = 2,

    /// <summary>Comfortably charged: the success colour. Only a charge meter says this.</summary>
    Healthy = 3,
}

/// <summary>Which way round a meter's thresholds run.</summary>
public enum PressureScale
{
    /// <summary>Always the accent colour (throughput, latency: no "too much" a meter can know).</summary>
    None = 0,

    /// <summary>A load: caution from 70%, critical from 90% (processor, memory).</summary>
    Usage = 1,

    /// <summary>A charge running down: healthy above 50%, caution to 21%, critical at 20% and under.</summary>
    Charge = 3,
}

/// <summary>
/// [S-1123] The original's usage colours: cyan under 70%, amber 70–89%, red from 90% (battery green above 50%, amber 21–50%, red at 20% and under, accent while charging).
/// <para>
/// The colour goes on the chart, the bar and secondary bars — never on the reading itself, which
/// stays the primary text colour so it is legible whatever the load (S-432).
/// </para>
/// </summary>
public static class MeterPressures
{
    /// <summary>The pressure of <paramref name="fraction"/> (0..1) on <paramref name="scale"/>; normal when unknown.</summary>
    public static MeterPressure For(PressureScale scale, double? fraction, bool charging = false)
    {
        if (fraction is not { } f || !double.IsFinite(f))
        {
            return MeterPressure.Normal;
        }

        // In whole percent, as the original compared: 0.895 reads "90%" and is coloured as it reads.
        var percent = Math.Round(Math.Clamp(f, 0, 1) * 100, MidpointRounding.AwayFromZero);

        return scale switch
        {
            PressureScale.Usage => percent >= 90 ? MeterPressure.Critical : percent >= 70 ? MeterPressure.Elevated : MeterPressure.Normal,

            // Charging is good news whatever the level, so it is the accent, not a warning.
            PressureScale.Charge when charging => MeterPressure.Normal,
            PressureScale.Charge => percent <= 20 ? MeterPressure.Critical : percent <= 50 ? MeterPressure.Elevated : MeterPressure.Healthy,
            _ => MeterPressure.Normal,
        };
    }
}
