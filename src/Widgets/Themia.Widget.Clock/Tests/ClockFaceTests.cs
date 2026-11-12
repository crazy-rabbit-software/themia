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

    /// <summary>The boxes of a clock's range: its Min, its Natural and its Max.</summary>
    private static IEnumerable<(double Width, double Height)> Range(ClockConfig config, string meridiem)
    {
        var sizing = ClockFace.Sizing(config, meridiem);
        return [Content(sizing.Min), Content(sizing.Natural), Content(sizing.Max)];
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void S6741_the_time_faces_hold_their_sizes_step_and_every_part_at_every_size_of_their_range(ReadoutSize size)
    {
        foreach (var style in new[] { ClockStyle.Digital, ClockStyle.Stacked, ClockStyle.Minimal })
        {
            foreach (var seconds in new[] { false, true })
            {
                var config = new ClockConfig { Size = size, Style = style, ShowSeconds = seconds };
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
        var config = new ClockConfig { Size = size, Style = ClockStyle.Flip };
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
        var config = new ClockConfig { Size = size, Style = ClockStyle.Analog };
        foreach (var (width, height) in Range(config, string.Empty))
        {
            var plan = ClockFace.Dial(width, height, line: true);

            Assert.True(plan.Line);
            Assert.InRange(plan.Dial, ClockFace.DialAt(size), ClockFace.DialAt(size) + ClockFace.Slack);
        }

        Assert.Equal(ClockFace.DialFloor, ClockFace.Dial(8, 8, line: false).Dial);
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void S6744_the_date_shows_its_day_on_its_sizes_step_with_both_words(ReadoutSize size)
    {
        var config = new ClockConfig { Size = size, Style = ClockStyle.Date };
        foreach (var (width, height) in Range(config, string.Empty))
        {
            var plan = ClockFace.Date(width, height);

            Assert.Equal(ReadoutSizes.Step(size), plan.Step);
            Assert.True(plan.Weekday && plan.Month);
            Assert.True(plan.Step.LineHeight + (2 * FamilyType.Under(plan.Step).LineHeight) <= height);
        }
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
}
