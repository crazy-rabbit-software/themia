using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2438]..[S-2443] [S-14500] The sizing contract's own rules: fit, invariants, the relax; no axis steps.</summary>
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
    public void S14500_every_size_in_range_is_its_own_no_axis_steps()
    {
        // Owner, 2026-11-20: "the widgets should not have the size increments anymore but should be incrementable in steps
        // of one": a list's height is any size from its least up, never a whole number of rows.
        var list = WidgetSizing.Range(new(220, 30), new(300, 180), new(520, 600));

        foreach (var height in new[] { 30.0, 40, 50, 100, 110, 177, 600 })
        {
            Assert.Equal(height, list.FitHeight(height));
        }

        Assert.Equal(30, list.FitHeight(0));
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
    public void S2441_a_sizing_out_of_order_says_what_is_wrong()
    {
        Assert.NotEmpty(WidgetSizing.Range(new(300, 100), new(200, 100), new(400, 100)).Problems());
        Assert.NotEmpty(WidgetSizing.Range(new(100, 100), new(200, 100), new(double.PositiveInfinity, 100)).Problems());
        Assert.NotEmpty(WidgetSizing.Range(new(0, 100), new(200, 100), new(400, 100)).Problems());
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
    public void S13012_a_kept_height_stays_and_no_room_keeps_its_size()
    {
        // A fence rolled up is its title bar: the relax leaves that height, and its width relaxes as any.
        var rolled = WidgetSizing.Range(new(152, 28), new(228, 28), new(456, 28)) with { KeepsHeight = true };
        Assert.Equal(28, SizeStretch.Relax(rolled).Max.Height);
        Assert.True(SizeStretch.Relax(rolled).FixedHeight);
        Assert.Equal(SizeStretch.Ceiling, SizeStretch.Relax(rolled).Max.Width);
        Assert.Empty(SizeStretch.Relax(rolled).Problems());

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

    /// <summary>A one-readout face of 200 × 100: its parts 176 × 76 in the inset of 12 on every side.</summary>
    private static WidgetSizing Face => SizeStretch.Relax(WidgetSizing.Range(new(48, 32), new(200, 100), new(400, 100)).ScalesAsOne());

    [Fact]
    public void S17100_a_face_that_scales_as_one_is_at_most_as_tall_as_its_natural_shape_at_its_width()
    {
        // Owner, 2026-11-23: "the height resize should be limited to not exceed where the spacings at the bottom or top are not
        // the same as they are in the other widgets too. to some extent limit the aspect ratio that the widget can have."
        Assert.Equal(StretchRoom.Face, Face.Room);
        Assert.Equal(100, Face.MostHeight(200), 6);
        Assert.Equal(24 + (352 * 76 / 176.0), Face.MostHeight(376), 6);
        Assert.Equal(32, Face.MostHeight(30), 6); // never under its least
        Assert.Equal(new SizeDip(376, 176), Face.Fit(new(376, 900)));
        Assert.Equal(new SizeDip(376, 150), Face.Fit(new(376, 150)));
        Assert.Empty(Face.Problems());
    }

    [Fact]
    public void S17100_its_width_stays_the_users_and_a_wider_card_stands_the_face_centred()
    {
        // The width is relaxed: a wider card holds the face by its height, the room at its sides alike (the owner's
        // "A reading that fills one side may stand centred on the other").
        Assert.Equal(SizeStretch.Ceiling, Face.Max.Width);
        Assert.Equal(new SizeDip(1000, 100), Face.Fit(new(1000, 100)));
    }

    [Fact]
    public void S17100_a_face_of_its_own_shape_and_a_line_over_it_follow_that_shape()
    {
        // A face whose natural is another arrangement (a metric's row, its readout stacked when taller) states its shape; a
        // line over it that keeps its size (a day line) is no part of the shape.
        var stacked = SizeStretch.Relax(WidgetSizing.Range(new(200, 30), new(240, 30), new(420, 30)).ScalesAsOne(new SizeDip(100, 50)));
        Assert.Equal(24 + (216 * 0.5), stacked.MostHeight(240), 6);

        var dayLine = SizeStretch.Relax(WidgetSizing.Range(new(48, 32), new(224, 124), new(224, 124)).ScalesAsOne(over: 24));
        Assert.Equal(124, dayLine.MostHeight(224), 6);
        Assert.Equal(48 + (400 * 76 / 200.0), dayLine.MostHeight(424), 6);
    }

    [Fact]
    public void S17101_a_fixed_extent_keeps_its_stated_most_on_that_axis()
    {
        // A row of controls, one item that does not repeat: growth stops at its natural, the width the user's.
        var item = SizeStretch.Relax(WidgetSizing.Range(new(220, 50), new(300, 50), new(520, 50)) with { KeepsHeight = true });
        Assert.Equal(50, item.Max.Height);
        Assert.Equal(SizeStretch.Ceiling, item.Max.Width);

        var column = SizeStretch.Relax(WidgetSizing.Range(new(60, 100), new(60, 200), new(60, 300)) with { KeepsWidth = true });
        Assert.Equal(60, column.Max.Width);
        Assert.Equal(SizeStretch.Ceiling, column.Max.Height);
        Assert.Equal(SizeStretch.Ceiling, column.MostHeight(60));
    }

    [Fact]
    public void S17102_content_that_grows_with_the_card_stays_relaxed()
    {
        foreach (var room in new[] { StretchRoom.Standard, StretchRoom.List, StretchRoom.Picture })
        {
            var grows = SizeStretch.Relax(Meter with { Room = room });
            Assert.Equal(SizeStretch.Ceiling, grows.MostHeight(160));
            Assert.Equal(new SizeDip(900, 900), grows.Fit(new(900, 900)));
        }
    }

    [Fact]
    public void S2448_the_large_size_checked_is_twice_the_natural_within_the_sizing()
    {
        var relaxed = SizeStretch.Relax(Meter);

        Assert.Equal(new SizeDip(440, 288), SizeStretch.Large(relaxed));
        Assert.Equal(new SizeDip(440, 200), SizeStretch.Large(Meter)); // held to a stated Max where one stands (a desktop icon's)

        // [S-17100] A face's large size is twice its natural width, its shape's height there in whole DIPs.
        Assert.Equal(new SizeDip(400, Math.Floor(24 + (376 * 76 / 176.0))), SizeStretch.Large(Face));
    }
}