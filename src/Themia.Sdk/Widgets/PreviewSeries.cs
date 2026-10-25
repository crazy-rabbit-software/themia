namespace Themia.Core.Widgets;

/// <summary>
/// [S-1917] The history a Debug render draws in place of live samples. Every name (a meter's kind,
/// a core, a second line) gets a curve of its own, so meters rendered side by side do not draw one
/// shape, and every run draws the same one.
/// </summary>
public static class PreviewSeries
{
    /// <summary>
    /// <paramref name="count"/> values around <paramref name="level"/>, never further from it than
    /// <paramref name="swing"/> and kept inside 0..1: four sines whose speeds, phases and weights
    /// come from the name, slow ones for load rising and falling and a quick, light one for the
    /// jitter a live reading has. The seed is
    /// the name's FNV-1a hash, the same on every run (a string's own hash is not).
    /// </summary>
    public static IReadOnlyList<double> Wave(string name, int count, double level, double swing)
    {
        ArgumentNullException.ThrowIfNull(name);

        var seed = 2166136261u;
        foreach (var c in name)
        {
            seed = (seed ^ c) * 16777619u;
        }

        var random = new Random(unchecked((int)seed));
        var speeds = new double[4];
        var phases = new double[4];
        var weights = new double[4];
        for (var k = 0; k < 4; k++)
        {
            var quick = k == 3;
            speeds[k] = quick ? 1 / (0.8 + random.NextDouble()) : 1 / (1.6 + (random.NextDouble() * (3 + (k * 4))));
            phases[k] = random.NextDouble() * Math.Tau;
            weights[k] = quick ? 0.25 + (random.NextDouble() * 0.2) : 0.4 + random.NextDouble();
        }

        var total = weights.Sum();
        var values = new double[Math.Max(0, count)];
        for (var i = 0; i < values.Length; i++)
        {
            var wave = 0.0;
            for (var k = 0; k < 4; k++)
            {
                wave += weights[k] * Math.Sin((i * speeds[k]) + phases[k]);
            }

            values[i] = Math.Clamp(level + (swing * wave / total), 0, 1);
        }

        return values;
    }
}
