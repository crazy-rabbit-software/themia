using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2438]..[S-2443] The sizing contract's own rules: fit, steps, invariants, legacy.</summary>
public sealed class WidgetSizingTests
{
    private static WidgetSizing Meter => WidgetSizing.Range(new(160, 112), new(220, 144), new(480, 200));

    [Fact]
    public void S2440_a_size_is_held_to_min_and_max_on_each_axis()
    {
        Assert.Equal(new SizeDip(160, 200), Meter.Fit(new(100, 900)));
        Assert.Equal(new SizeDip(300, 150), Meter.Fit(new(300, 150)));
        Assert.Equal(Meter.Natural, Meter.Fit(new(double.NaN, double.PositiveInfinity)));
    }

    [Fact]
    public void S2439_a_stepped_axis_lands_on_its_nearest_step_in_range()
    {
        var steps = new SizeSteps(Base: 76, Step: 28, MinCount: 1, MaxCount: 20);
        var list = WidgetSizing.Range(new(220, 1), new(300, 1), new(520, 1)).WithHeightSteps(steps);

        Assert.Equal(104, list.Min.Height);
        Assert.Equal(636, list.Max.Height);
        Assert.Equal(76 + (28 * 3), list.FitHeight(76 + (28 * 3) + 10));
        Assert.Equal(76 + (28 * 4), list.FitHeight(76 + (28 * 3) + 15));
        Assert.Equal(104, list.FitHeight(0));
        Assert.Empty(list.Problems());
    }

    [Fact]
    public void S2439_an_axis_that_follows_the_config_is_the_configs_count_whatever_is_asked()
    {
        var steps = new SizeSteps(76, 28, 1, 20, Count: 6);
        var list = WidgetSizing.Range(new(220, 1), new(300, 1), new(520, 1)).WithHeightSteps(steps);

        Assert.Equal(76 + (6 * 28), list.Natural.Height);
        Assert.Equal(76 + (6 * 28), list.FitHeight(900));
        Assert.Equal(76 + (6 * 28), list.FitHeight(10));
        Assert.Equal(3, steps.Nearest(76 + (3 * 28) + 2));
        Assert.Empty(list.Problems());
    }

    [Fact]
    public void S2438_an_axis_whose_min_is_its_max_is_fixed()
    {
        var face = WidgetSizing.Range(new(200, 100), new(200, 100), new(400, 100));
        Assert.True(face.FixedHeight);
        Assert.False(face.FixedWidth);
        Assert.True(WidgetSizing.Fixed(new(96, 96)) is { FixedWidth: true, FixedHeight: true });
    }

    [Fact]
    public void S2441_a_sizing_out_of_order_or_off_its_steps_says_what_is_wrong()
    {
        Assert.NotEmpty(WidgetSizing.Range(new(300, 100), new(200, 100), new(400, 100)).Problems());
        Assert.NotEmpty(WidgetSizing.Range(new(100, 100), new(200, 100), new(double.PositiveInfinity, 100)).Problems());
        Assert.NotEmpty(WidgetSizing.Range(new(0, 100), new(200, 100), new(400, 100)).Problems());
        Assert.NotEmpty((Meter with { HeightSteps = new SizeSteps(76, 28, 1, 20) }).Problems());
        Assert.NotEmpty(Meter.WithHeightSteps(new SizeSteps(76, 0, 1, 20)).Problems());
        Assert.NotEmpty(Meter.WithHeightSteps(new SizeSteps(76, 28, 1, 20, Count: 30)).Problems());
        Assert.Empty(Meter.Problems());
    }

    [Fact]
    public void S2443_a_legacy_sizing_is_the_old_way_and_fits_nothing()
    {
        var legacy = WidgetSizing.Legacy(280, 140);

        Assert.True(legacy.IsLegacy);
        Assert.Equal(new SizeDip(32, 32), legacy.Min);
        Assert.Equal(new SizeDip(280, 140), legacy.Natural);
        Assert.Equal(SizeDip.Unbounded, legacy.Max);
        Assert.Equal(new SizeDip(5000, 10), legacy.Fit(new(5000, 10)));
        Assert.Empty(legacy.Problems());
    }
}
