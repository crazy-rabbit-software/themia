namespace Themia.Core.Widgets;

/// <summary>
/// [S-6263] The ping style's signal bars, as Windows draws a Wi-Fi signal: four bars rising left to
/// right, as many lit as the latency deserves.
/// </summary>
public static class PingSignal
{
    public const int BarCount = 4;

    /// <summary>
    /// The latencies, in milliseconds, at or under which an answer earns 4, 3 and 2 bars: up to 50 ms is
    /// as good as a connection gets for a call or a game, up to 100 ms is fine for anything but a
    /// fast game, up to 200 ms is felt in a call; past that one bar. An unanswered probe is none.
    /// </summary>
    public static readonly IReadOnlyList<double> Thresholds = [50, 100, 200];

    /// <summary>
    /// How many bars a probe earns: none for an unanswered one (null, NaN, negative), else 4 down
    /// to 1 by <see cref="Thresholds"/>.
    /// </summary>
    public static int Level(double? latencyMs)
    {
        if (latencyMs is not { } ms || double.IsNaN(ms) || ms < 0)
        {
            return 0;
        }

        var bars = BarCount;
        foreach (var threshold in Thresholds)
        {
            if (ms <= threshold)
            {
                return bars;
            }

            bars--;
        }

        return bars;
    }
}
