using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1917] The Debug renders' fixture histories: one curve per name, the same every run.</summary>
public class PreviewSeriesTests
{
    [Fact]
    public void S1917_a_name_draws_the_same_curve_every_time_and_another_name_another()
    {
        var cpu = PreviewSeries.Wave("cpu", 60, 0.4, 0.3);

        Assert.Equal(cpu, PreviewSeries.Wave("cpu", 60, 0.4, 0.3));
        Assert.NotEqual(cpu, PreviewSeries.Wave("memory", 60, 0.4, 0.3));
    }

    [Theory]
    [InlineData(0.05, 0.3)]
    [InlineData(0.5, 0.3)]
    [InlineData(0.95, 0.3)]
    public void S1917_the_curve_stays_within_its_swing_and_inside_0_to_1(double level, double swing)
    {
        var values = PreviewSeries.Wave("gpu", 120, level, swing);

        Assert.Equal(120, values.Count);
        Assert.All(values, v => Assert.InRange(v, Math.Max(0, level - swing), Math.Min(1, level + swing)));
        Assert.True(values.Max() - values.Min() > swing / 4, "a curve, not a flat line");
    }
}
