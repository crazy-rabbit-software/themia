using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2438]..[S-2443] The sizing contract's own rules: fit, steps, invariants, legacy.</summary>
public sealed class WidgetSizingTests
{
    private static WidgetSizing Meter => WidgetSizing.Range(new(160, 112), new(220, 144), new(480, 200));

    [Fact]
    public void S2438_a_size_is_held_to_min_and_max_on_each_axis()
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
    public void S2441_a_sizing_without_a_finite_maximum_does_not_hold()
    {
        var unbounded = WidgetSizing.Range(new(32, 32), new(280, 140), new(double.PositiveInfinity, double.PositiveInfinity));

        Assert.NotEmpty(unbounded.Problems());
    }

    [Fact]
    public void S12411_a_relaxed_range_has_no_practical_max_and_keeps_min_and_natural()
    {
        // The owner, 2026-11-16: "maximum sizes should not really be a thing as this is for the user to decide".
        foreach (var room in new[] { StretchRoom.Standard, StretchRoom.List, StretchRoom.Picture })
        {
            var relaxed = SizeStretch.Relax(Meter with { Room = room });

            Assert.Equal((Meter.Min, Meter.Natural), (relaxed.Min, relaxed.Natural));
            Assert.Equal(new SizeDip(SizeStretch.Ceiling, SizeStretch.Ceiling), relaxed.Max);
            Assert.Empty(relaxed.Problems());
        }
    }

    [Fact]
    public void S12411_a_fixed_axis_is_relaxed_too()
    {
        var reading = SizeStretch.Relax(WidgetSizing.Range(new(120, 84), new(160, 84), new(480, 84)));

        Assert.Equal(new SizeDip(SizeStretch.Ceiling, SizeStretch.Ceiling), reading.Max);
        Assert.False(reading.FixedHeight);
    }

    [Fact]
    public void S12411_a_stepped_axis_keeps_its_steps_and_no_room_keeps_its_size()
    {
        var rows = new SizeSteps(48, 28, 1, 20, 6);
        var stepped = WidgetSizing.Range(new(220, 1), new(300, 1), new(520, 1)).WithHeightSteps(rows);
        Assert.Equal(stepped.Max.Height, SizeStretch.Relax(stepped).Max.Height);
        Assert.Equal(SizeStretch.Ceiling, SizeStretch.Relax(stepped).Max.Width);
        Assert.Empty(SizeStretch.Relax(stepped).Problems());

        var icon = WidgetSizing.Fixed(new(76, 100)) with { Room = StretchRoom.None };
        Assert.Same(icon, SizeStretch.Relax(icon));
    }

    [Fact]
    public void S12411_the_relaxed_max_is_finite_never_lowers_a_stated_one_and_relaxing_twice_is_once()
    {
        Assert.True(double.IsFinite(SizeStretch.Ceiling));

        var stated = WidgetSizing.Range(new(100, 100), new(100, 100), new(5000, 5000));
        Assert.Equal(stated.Max, SizeStretch.Relax(stated).Max);

        Assert.Equal(SizeStretch.Relax(Meter), SizeStretch.Relax(SizeStretch.Relax(Meter)));
    }

    [Fact]
    public void S2448_the_large_size_checked_is_twice_the_natural_within_the_sizing()
    {
        var relaxed = SizeStretch.Relax(Meter);

        Assert.Equal(new SizeDip(440, 288), SizeStretch.Large(relaxed));
        Assert.Equal(new SizeDip(440, 200), SizeStretch.Large(Meter)); // held to a stated Max where one stands (a desktop icon's)
    }
}