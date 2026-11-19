using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Clock.Tests;

/// <summary>
/// [S-6741]..[S-6744] [S-2449] The clock's faces on the family's steps: the step its Size names, every part
/// its config asks for, at every size of its range ([S-11800]).
/// </summary>
public sealed class ClockFaceTests
{
    public static TheoryData<ReadoutSize> Sizes() => [ReadoutSize.Small, ReadoutSize.Medium, ReadoutSize.Large];

    /// <summary>The content box a card of <paramref name="box"/> gives the face: the card less its inset of 12 (the clock has no header).</summary>
    private static (double Width, double Height) Content(SizeDip box) => (box.Width - (2 * FamilySpace.Inset), box.Height - (2 * FamilySpace.Inset));

    /// <summary>
    /// The boxes of a clock's range the face is planned in at its step: its Natural and its Max (under its Natural the
    /// face is the same plan scaled down, [S-12833]).
    /// </summary>
    private static IEnumerable<(double Width, double Height)> Range(ClockConfig config, string meridiem)
    {
        var sizing = ClockFace.Sizing(config, meridiem);
        return [Content(sizing.Natural), Content(sizing.Max)];
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void S6741_S12833_a_time_face_scales_down_until_its_smallest_text_is_at_the_floor(ReadoutSize size)
    {
        // Owner, 2026-11-19: "you should be able to make the clock even smaller than it is right now".
        foreach (var style in new[] { ClockStyle.Digital, ClockStyle.Stacked, ClockStyle.Minimal })
        {
            var config = new ClockConfig { Size = size, Style = style, OffsetMode = ClockOffsetMode.Utc };
            var sizing = ClockFace.Sizing(config, "AM");

            Assert.True(sizing.Min.Height < sizing.Natural.Height, $"{style} {size}: {sizing.Min.Height} not under {sizing.Natural.Height}");
            Assert.True(sizing.Min.Width < sizing.Natural.Width);
            Assert.True(sizing.Min.Height >= WidgetSizing.LeastScaled && sizing.Min.Width >= WidgetSizing.LeastScaled);
        }

        // At its least, the smallest text (the meridiem's unit, the line under) is drawn at the floor, 9.
        var plain = ClockFace.Sizing(new ClockConfig { Size = size, Style = ClockStyle.Minimal }, string.Empty);
        var step = ReadoutSizes.Step(size);
        Assert.Equal(Math.Max(WidgetSizing.LeastScaled, Math.Ceiling(plain.Natural.Height * ScaleFloor.Text / step.FontSize)), plain.Min.Height, 6);
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void S6741_the_time_faces_hold_their_sizes_step_and_every_part_at_every_size_of_their_range(ReadoutSize size)
    {
        foreach (var style in new[] { ClockStyle.Digital, ClockStyle.Stacked, ClockStyle.Minimal })
        {
            foreach (var seconds in new[] { false, true })
            {
                var config = new ClockConfig { Size = size, Style = style, ShowSeconds = seconds, OffsetMode = ClockOffsetMode.Utc };
                var under = style != ClockStyle.Minimal;
                foreach (var (width, height) in Range(config, "AM"))
                {
                    var plan = ClockFace.Readout(width, height, style, config.DrawsSeconds, "AM", under);

                    Assert.Equal(ReadoutSizes.Step(size), plan.Step);
                    Assert.Equal(style == ClockStyle.Stacked, plan.Stacked);
                    Assert.True(plan.Meridiem, $"{size} {style} {width}x{height}");
                    Assert.Equal(config.DrawsSeconds, plan.Seconds);
                    Assert.Equal(under, plan.Under);
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void S6742_the_flip_cards_hold_their_sizes_step_in_a_row_at_every_size_of_their_range(ReadoutSize size)
    {
        var config = new ClockConfig { Size = size, Style = ClockStyle.Flip, OffsetMode = ClockOffsetMode.Utc };
        foreach (var (width, height) in Range(config, "AM"))
        {
            var (plan, vertical, meridiem) = ClockFace.Flip(width, height, 2, "AM", under: true);

            Assert.Equal(ReadoutSizes.Step(size), plan.Step);
            Assert.False(vertical);
            Assert.True(meridiem && plan.Under);
            Assert.Equal(FlipRules.CardWidth(plan.Step, ClockFace.Figures), plan.CardWidth);
        }
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void S6743_the_dial_is_its_sizes_and_keeps_its_line(ReadoutSize size)
    {
        var config = new ClockConfig { Size = size, Style = ClockStyle.Analog, OffsetMode = ClockOffsetMode.Utc };
        foreach (var (width, height) in Range(config, string.Empty))
        {
            var plan = ClockFace.Dial(width, height, line: true);

            Assert.True(plan.Line);
            Assert.InRange(plan.Dial, ClockFace.DialAt(size), ClockFace.DialAt(size) + ClockFace.Slack);
        }

        Assert.Equal(ClockFace.DialFloor, ClockFace.Dial(8, 8, line: false).Dial);
    }

    [Fact]
    public void S6741_the_line_under_holds_the_city_and_the_offset_as_its_room_does_never_a_date()
    {
        // [S-12805] The date is the Date widget's: the clock's line says where (a world clock) and the offset.
        var caption = FamilyType.Caption;
        Assert.Equal("Tokyo · UTC+9", ClockFace.Under(1000, caption, "Tokyo", "UTC+9"));
        Assert.Equal("Tokyo", ClockFace.Under(30, caption, "Tokyo", "UTC+9"));
        Assert.Equal("UTC+9", ClockFace.Under(1000, caption, null, "UTC+9"));
        Assert.Null(ClockFace.Under(100, caption, null, null));
    }

    [Fact]
    public void S6011_the_clock_has_no_date_style_and_no_date_line()
    {
        Assert.DoesNotContain("Date", Enum.GetNames<ClockStyle>());
        Assert.DoesNotContain("Date", Enum.GetNames<ClockSetting>());
        Assert.Null(typeof(ClockConfig).GetProperty("ShowDate"));

        // A plain clock has no line under its face: its natural box is the face's step alone in the inset.
        var plain = ClockFace.Sizing(new ClockConfig(), string.Empty).Natural;
        Assert.Equal(ReadoutSizes.Step(ReadoutSizes.Default).LineHeight + 24 + ClockFace.Slack, plain.Height);
    }
}
