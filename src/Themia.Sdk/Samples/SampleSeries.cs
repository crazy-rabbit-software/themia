#if DEBUG
namespace Themia.Sdk.Samples;

/// <summary>
/// Debug renders only ([S-2056]): a believable price history for a sample (an exchange rate, a
/// stock): a seeded random walk of small moves, not a function plot, the same for a name on every run.
/// </summary>
public static class SampleSeries
{
    /// <summary>
    /// <paramref name="count"/> values, oldest first, ending at <paramref name="last"/>: each step a
    /// normally distributed move of about <paramref name="move"/> (a fraction: 0.003 is 0.3 %), held
    /// back towards the series' middle so it wanders rather than runs away; never more than four
    /// moves in one step. The seed is the name's FNV-1a hash.
    /// </summary>
    public static IReadOnlyList<double> Walk(string name, int count, double last, double move)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var seed = 2166136261u;
        foreach (var c in name)
        {
            seed = (seed ^ c) * 16777619u;
        }

        var random = new Random(unchecked((int)seed));
        var logs = new double[count];
        var level = 0.0;

        for (var i = 1; i < count; i++)
        {
            // Box-Muller: a standard normal from two uniforms, cut at four.
            var u = 1 - random.NextDouble();
            var v = random.NextDouble();
            var normal = Math.Clamp(Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(Math.Tau * v), -4, 4);
            level += (normal * move) - (level * 0.03);
            logs[i] = level;
        }

        var end = count > 0 ? logs[^1] : 0;
        return [.. logs.Select(l => last * Math.Exp(l - end))];
    }
}
#endif
