using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Clock.Tests;

/// <summary>[S-6741]..[S-6744] The clock's faces on the family's steps (group G5): fixed steps, never filling the box.</summary>
public sealed class ClockFaceTests
{
    /// <summary>A tier as the clock lays it out: the card's area and its content box (the card less 16 on every side).</summary>
    private static (WidgetArea Area, double Width, double Height) Tier(double w, double h) => (new WidgetArea(w, h), w - 32, h - 32);

    private static ClockReadoutPlan Readout(double w, double h, ClockStyle style = ClockStyle.Digital, bool seconds = false, string meridiem = "AM", bool under = true)
    {
        var (area, width, height) = Tier(w, h);
        return ClockFace.Readout(area, width, height, style, seconds, meridiem, under);
    }

    [Theory]
    [InlineData(320, 240, 68)]
    [InlineData(480, 160, 68)]
    [InlineData(560, 420, 136)]
    [InlineData(800, 560, 192)]
    public void S6741_the_digital_clock_takes_the_largest_hero_step_that_fits_never_a_size_of_its_own(double w, double h, double expected)
    {
        var plan = Readout(w, h);

        Assert.Equal(expected, FamilyType.BaseOf(plan.Step));
        Assert.False(plan.Stacked);
        Assert.True(plan.Meridiem && plan.Under);
    }

    [Fact]
    public void S6741_a_row_too_wide_for_the_floor_step_stacks_and_a_tall_column_stacks_where_that_is_larger()
    {
        // XS: "88:88 AM" on 28 is 99 wide in 88; the hours over the minutes fit.
        var xs = Readout(120, 120);
        Assert.True(xs.Stacked);
        Assert.Equal(FamilyType.Big28, xs.Step);
        Assert.True(xs.Meridiem);

        // Tall: one line holds 40, two lines 68.
        var tall = Readout(200, 400);
        Assert.True(tall.Stacked);
        Assert.Equal(68, FamilyType.BaseOf(tall.Step));
        Assert.Equal(2, tall.Lines);

        // The Stacked style always stacks; with seconds it is three lines.
        Assert.True(Readout(800, 560, ClockStyle.Stacked).Stacked);
        Assert.Equal(3, Readout(800, 560, ClockStyle.Stacked, seconds: true).Lines);
    }

    [Fact]
    public void S6741_where_nothing_fits_the_floor_step_the_meridiem_gives_way_then_the_seconds()
    {
        // 50 wide: no row, no stack with "AM" (60); "88" alone stacks.
        var (area, _, _) = Tier(92, 160);
        var plan = ClockFace.Readout(area, 50, 128, ClockStyle.Digital, seconds: true, "AM", under: false);
        Assert.False(plan.Meridiem);
        Assert.True(plan.Stacked);

        var narrow = ClockFace.Readout(area, 20, 128, ClockStyle.Digital, seconds: true, "AM", under: false);
        Assert.False(narrow.Seconds);
        Assert.False(narrow.Meridiem);
    }

    [Fact]
    public void S6741_a_box_too_short_for_the_line_under_sheds_it_and_the_minimal_clock_has_none()
    {
        Assert.False(Readout(200, 82).Under);
        Assert.False(Readout(800, 560, ClockStyle.Minimal, under: false).Under);
    }

    [Fact]
    public void S6741_the_line_under_holds_the_city_the_date_and_the_offset_as_its_room_does()
    {
        var caption = FamilyType.Caption;
        Assert.Equal("Tokyo · Thursday, October 29 · UTC+9", ClockFace.Under(1000, caption, "Tokyo", "Thursday, October 29", "Thu, Oct 29", "UTC+9"));
        Assert.Equal("Tokyo · Thu, Oct 29 · UTC+9", ClockFace.Under(180, caption, "Tokyo", "Thursday, October 29", "Thu, Oct 29", "UTC+9"));
        Assert.Equal("Tokyo · Thu, Oct 29", ClockFace.Under(130, caption, "Tokyo", "Thursday, October 29", "Thu, Oct 29", "UTC+9"));
        Assert.Equal("Tokyo", ClockFace.Under(60, caption, "Tokyo", "Thursday, October 29", "Thu, Oct 29", "UTC+9"));
        Assert.Equal("Thu, Oct 29", ClockFace.Under(10, caption, null, "Thursday, October 29", "Thu, Oct 29", null));
        Assert.Null(ClockFace.Under(100, caption, null, null, null, null));
    }

    [Fact]
    public void S6742_flip_cards_take_a_step_with_the_meridiem_beside_them_and_stand_in_a_column_where_a_row_does_not_fit()
    {
        var (area, width, height) = Tier(320, 240);
        var (plan, vertical, meridiem) = ClockFace.Flip(area, width, height, 2, "AM", under: true);
        Assert.Equal(68, FamilyType.BaseOf(plan.Step));
        Assert.False(vertical);
        Assert.True(meridiem && plan.Under);
        Assert.Equal(FlipRules.CardWidth(plan.Step, "88"), plan.CardWidth);

        var (xsArea, xsWidth, xsHeight) = Tier(120, 120);
        var (xs, xsVertical, xsMeridiem) = ClockFace.Flip(xsArea, xsWidth, xsHeight, 2, "AM", under: true);
        Assert.True(xsVertical);
        Assert.False(xsMeridiem);
        Assert.False(xs.Under);
        Assert.Equal(FamilyType.Big28, xs.Step);

        var (tallArea, tallWidth, tallHeight) = Tier(200, 400);
        Assert.True(ClockFace.Flip(tallArea, tallWidth, tallHeight, 2, "AM", under: true).Vertical);
    }

    [Fact]
    public void S6743_a_dial_keeps_its_line_from_96_and_a_strip_puts_the_line_beside_it()
    {
        var (sArea, sWidth, sHeight) = Tier(200, 150);
        var s = ClockFace.Dial(sArea, sWidth, sHeight, line: true);
        Assert.False(s.Line);
        Assert.Equal(118, s.Dial);

        var (mArea, mWidth, mHeight) = Tier(320, 240);
        var m = ClockFace.Dial(mArea, mWidth, mHeight, line: true);
        Assert.True(m.Line && !m.Side);
        Assert.Equal(208 - 8 - 20, m.Dial);

        var (wideArea, wideWidth, wideHeight) = Tier(480, 160);
        var wide = ClockFace.Dial(wideArea, wideWidth, wideHeight, line: true);
        Assert.True(wide.Side);
        Assert.Equal(128, wide.Dial);
        Assert.Equal(448 - 128 - 16, wide.LineWidth);

        Assert.Equal(ClockFace.DialFloor, ClockFace.Dial(new WidgetArea(40, 40), 8, 8, line: false).Dial);
    }

    [Fact]
    public void S6744_the_day_is_a_readout_on_a_family_step_with_its_words_on_its_line_under()
    {
        var (area, width, height) = Tier(560, 420);
        var plan = ClockFace.Date(area, width, height);

        Assert.Equal(192, FamilyType.BaseOf(plan.Step));
        Assert.Equal(DateFace.Column, plan.Face);
        Assert.True(plan.Weekday && plan.Month);
        Assert.True(plan.Step.LineHeight + (2 * FamilyType.Under(plan.Step).LineHeight) <= height);
    }
}
