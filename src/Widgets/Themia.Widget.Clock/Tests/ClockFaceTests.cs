using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Clock.Tests;

/// <summary>
/// [S-6741]..[S-6744] The clock's faces on the family's steps: the one-readout step (no Size, [S-2449] retired),
/// every part its config asks for, at every size of its range ([S-11800]).
/// </summary>
public sealed class ClockFaceTests
{
    /// <summary>
    /// [S-12833] The face as its sizing states it (<see cref="ClockFace.Plan"/>, estimated) in a content box: what the least
    /// box is made of. The view decides on the face as drawn (<c>ReadingFit</c>).
    /// </summary>
    private static FaceFit Fit(double width, double height, ClockConfig config, string meridiem)
        => config.Style == ClockStyle.Analog
            ? FaceFit.All
            : BlockLayout.Decide(width, height, ClockFace.PartSizes(config, meridiem), floor => ClockFace.Plan(config, meridiem, floor));


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

    /// <summary>Every clock a config can make: each style, seconds, 12- or 24-hour time, with or without the line under.</summary>
    private static IEnumerable<(ClockConfig Config, string Meridiem)> Clocks()
    {
        foreach (var style in Enum.GetValues<ClockStyle>())
        {
            foreach (var seconds in new[] { false, true })
            {
                foreach (var offset in new[] { ClockOffsetMode.None, ClockOffsetMode.Utc })
                {
                    foreach (var meridiem in new[] { string.Empty, "AM" })
                    {
                        yield return (new ClockConfig { Style = style, ShowSeconds = seconds, OffsetMode = offset }, meridiem);
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(ClockStyle.Digital, 32, 89)]
    [InlineData(ClockStyle.Minimal, 32, 38)]
    [InlineData(ClockStyle.Flip, 40, 89)]
    [InlineData(ClockStyle.Stacked, 56, 208)]
    [InlineData(ClockStyle.Word, 70, 414)]
    [InlineData(ClockStyle.Analog, 64, 246)]
    public void S6011_S12833_the_clocks_least_height_is_its_figures_at_the_floor_never_its_meridiem_or_line_under(ClockStyle style, double most, double before)
    {
        // Owner, 2026-11-19: "the clock has a min height that is way to big". Before, the meridiem's unit or the line under
        // at 9 held the figures large (a world clock 89 tall, a stacked one with seconds 208) and the dial and the words did
        // not scale down at all (170, 234 at the step now the only one); `before` is the tallest least height the style had
        // when it had a Size (Large Word: 414).
        foreach (var (config, meridiem) in Clocks().Where(c => c.Config.Style == style))
        {
            var sizing = ClockFace.Sizing(config, meridiem);
            var name = $"{style} seconds {config.ShowSeconds} {config.OffsetMode} '{meridiem}'";

            Assert.InRange(sizing.Min.Height, WidgetSizing.LeastScaled, most);
            Assert.True(sizing.Min.Height < before, name);
            Assert.True(sizing.Min.Width >= WidgetSizing.LeastScaled && sizing.Min.Width < sizing.Natural.Width, name);
            Assert.Empty(sizing.Problems());
        }
    }

    [Fact]
    public void S6741_S12833_a_time_faces_least_box_is_its_figures_alone_at_9_in_the_cards_inset()
    {
        // The figures are the face: at the least box they are 9 DIP, whatever the meridiem and the line under.
        foreach (var meridiem in new[] { string.Empty, "AM" })
        {
            var config = new ClockConfig { OffsetMode = ClockOffsetMode.Utc };
            var sizing = ClockFace.Sizing(config, meridiem);
            var figures = HeroFit.Ems("88:88") * ScaleFloor.Text;

            Assert.Equal(Math.Ceiling(figures) + (2 * FamilySpace.Inset), sizing.Min.Width, 6);
            Assert.Equal(WidgetSizing.LeastScaled, sizing.Min.Height);
        }
    }

    [Fact]
    public void S6743_S12833_the_dial_goes_down_to_its_floor_in_the_inset()
    {
        var sizing = ClockFace.Sizing(new ClockConfig { Style = ClockStyle.Analog, OffsetMode = ClockOffsetMode.Utc }, string.Empty);
        var least = ClockFace.DialFloor + (2 * FamilySpace.Inset);

        Assert.Equal(new SizeDip(least, least), sizing.Min);
        Assert.Equal(ClockFace.DialFloor, ClockFace.Dial(least - (2 * FamilySpace.Inset), least - (2 * FamilySpace.Inset), line: true).Dial);
    }

    [Fact]
    public void S6741_S12833_every_part_is_drawn_at_the_natural_box_and_only_the_figures_at_the_least()
    {
        foreach (var (config, meridiem) in Clocks().Where(c => c.Config.Style != ClockStyle.Analog))
        {
            var sizing = ClockFace.Sizing(config, meridiem);
            var parts = ClockFace.PartSizes(config, meridiem);
            var figures = parts.Max();

            var natural = Fit(sizing.Natural.Width - (2 * FamilySpace.Inset), sizing.Natural.Height - (2 * FamilySpace.Inset), config, meridiem);
            Assert.All(parts, size => Assert.True(natural.Draws(size), $"{config.Style}: {size} gave way at its natural box"));

            var least = Fit(sizing.Min.Width - (2 * FamilySpace.Inset), sizing.Min.Height - (2 * FamilySpace.Inset), config, meridiem);
            Assert.All(parts.Where(size => size < figures), size => Assert.False(least.Draws(size)));
            Assert.True(least.Draws(figures));
            Assert.InRange(least.Scale * figures, ScaleFloor.Text - 1e-6, ScaleFloor.Text + 3);
        }
    }

    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(1.0, 0.0)]
    [InlineData(0.0, 1.0)]
    public void S6741_S12833_as_the_card_gets_smaller_no_part_comes_back_none_is_under_9_and_the_rest_fills_it(double byWidth, double byHeight)
    {
        // A card made smaller from its natural box to its least: proportionally, by its width or by its height alone.
        foreach (var (config, meridiem) in Clocks().Where(c => c.Config.Style != ClockStyle.Analog))
        {
            var sizing = ClockFace.Sizing(config, meridiem);
            var parts = ClockFace.PartSizes(config, meridiem);
            var least = ScaleFloor.Least(parts.Max());
            double lastFloor = 0;
            for (var t = 1.0; t >= 0; t -= 0.02)
            {
                var width = (byWidth > 0 ? sizing.Min.Width + (t * (sizing.Natural.Width - sizing.Min.Width)) : sizing.Natural.Width) - (2 * FamilySpace.Inset);
                var height = (byHeight > 0 ? sizing.Min.Height + (t * (sizing.Natural.Height - sizing.Min.Height)) : sizing.Natural.Height) - (2 * FamilySpace.Inset);
                var fit = Fit(width, height, config, meridiem);
                var name = $"{config.Style} {config.ShowSeconds} {config.OffsetMode} '{meridiem}' {width}x{height}";

                Assert.True(fit.Floor >= lastFloor, $"{name}: a part came back");
                Assert.All(parts.Where(fit.Draws), size => Assert.True(size * fit.Scale >= ScaleFloor.Text - 1e-6, $"{name}: {size} at {size * fit.Scale}"));

                // [S-12833] What is drawn fills the room on one axis (or stands at the reading's floor).
                var plan = ClockFace.Plan(config, meridiem, fit.Floor);
                Assert.True(Math.Abs(fit.Scale - least) < 1e-9 || Math.Abs((fit.Scale * plan.Width) - width) < 1e-6 || Math.Abs((fit.Scale * plan.Ink) - height) < 1e-6, $"{name}: {fit.Scale} fills neither axis");
                lastFloor = fit.Floor;
            }
        }
    }

    [Fact]
    public void S6741_S12833_a_world_clock_whose_city_line_gave_way_fills_its_card()
    {
        // The world style at 350x70: the card gives the top inset, the view the sides and the foot, so the face's room is
        // 326 x 46. The city line would be drawn under 9 and goes; held at the scale it went at (9 / 14) the figures stood
        // 83 from either side and short of the top and foot. They grow back into its room until the height stops them.
        var config = new ClockConfig { TimeZoneId = "Asia/Tokyo", OffsetMode = ClockOffsetMode.Utc, ShowSeconds = true };
        Assert.True(config.IsWorldClock);
        var fit = Fit(326, 46, config, "AM");
        Assert.False(fit.Draws(FamilyType.Under(ClockFace.Step).FontSize));

        var plan = ClockFace.Plan(config, "AM", fit.Floor);
        Assert.Equal(Math.Min(326 / plan.Width, 46 / plan.Ink), fit.Scale, 6);
        Assert.True(fit.Scale > 9 / 14.0 + 0.1);
    }

    [Fact]
    public void S6741_S12833_the_line_under_gives_way_before_the_meridiem()
    {
        var config = new ClockConfig { OffsetMode = ClockOffsetMode.Utc };
        var sizing = ClockFace.Sizing(config, "AM");
        var step = ClockFace.Step;
        var room = (W: sizing.Natural.Width - (2 * FamilySpace.Inset), H: sizing.Natural.Height - (2 * FamilySpace.Inset));

        // Half the card: the line under (14) would be 7; the meridiem (28) still 14.
        var half = Fit(room.W / 2, room.H / 2, config, "AM");
        Assert.False(half.Draws(FamilyType.Under(step).FontSize));
        Assert.True(half.Draws(FamilyType.Unit(step).FontSize));
    }

    [Fact]
    public void S6741_the_time_faces_hold_their_step_and_every_part_at_every_size_of_their_range()
    {
        foreach (var style in new[] { ClockStyle.Digital, ClockStyle.Stacked, ClockStyle.Minimal })
        {
            foreach (var seconds in new[] { false, true })
            {
                var config = new ClockConfig { Style = style, ShowSeconds = seconds, OffsetMode = ClockOffsetMode.Utc };
                var under = style != ClockStyle.Minimal;
                foreach (var (width, height) in Range(config, "AM"))
                {
                    var plan = ClockFace.Readout(width, height, style, config.DrawsSeconds, "AM", under);

                    Assert.Equal(ClockFace.Step, plan.Step);
                    Assert.Equal(style == ClockStyle.Stacked, plan.Stacked);
                    Assert.True(plan.Meridiem, $"{style} {width}x{height}");
                    Assert.Equal(config.DrawsSeconds, plan.Seconds);
                    Assert.Equal(under, plan.Under);
                }
            }
        }
    }

    [Fact]
    public void S6742_the_flip_cards_hold_their_step_in_a_row_at_every_size_of_their_range()
    {
        var config = new ClockConfig { Style = ClockStyle.Flip, OffsetMode = ClockOffsetMode.Utc };
        foreach (var (width, height) in Range(config, "AM"))
        {
            var (plan, vertical, meridiem) = ClockFace.Flip(width, height, 2, "AM", under: true);

            Assert.Equal(ClockFace.Step, plan.Step);
            Assert.False(vertical);
            Assert.True(meridiem && plan.Under);
            Assert.Equal(FlipRules.CardWidth(plan.Step, ClockFace.Figures), plan.CardWidth);
        }
    }

    [Fact]
    public void S6743_the_dial_is_its_side_and_keeps_its_line()
    {
        var config = new ClockConfig { Style = ClockStyle.Analog, OffsetMode = ClockOffsetMode.Utc };
        foreach (var (width, height) in Range(config, string.Empty))
        {
            var plan = ClockFace.Dial(width, height, line: true);

            Assert.True(plan.Line);
            Assert.InRange(plan.Dial, ClockFace.DialSide, ClockFace.DialSide + LayoutMath.Slack);
        }

        Assert.Equal(ClockFace.DialFloor, ClockFace.Dial(8, 8, line: false).Dial);
    }

    [Fact]
    public void S6741_the_line_under_holds_the_city_and_the_offset_as_its_room_does_never_a_date()
    {
        // [S-12805] The date is the Date widget's: the clock's line says where (a world clock) and the offset.
        // [S-12833] Judged by the line as drawn (the view's measure), here 6 DIP a letter.
        static double Drawn(string text) => 6 * text.Length;
        Assert.Equal("Tokyo · UTC+9", ClockFace.Under(1000, Drawn, "Tokyo", "UTC+9"));
        Assert.Equal("Tokyo · UTC+9", ClockFace.Under(78, Drawn, "Tokyo", "UTC+9"));
        Assert.Equal("Tokyo", ClockFace.Under(77, Drawn, "Tokyo", "UTC+9"));
        Assert.Equal("UTC+9", ClockFace.Under(1000, Drawn, null, "UTC+9"));
        Assert.Null(ClockFace.Under(100, Drawn, null, null));
    }

    [Fact]
    public void S6011_the_clock_has_no_date_style_and_no_date_line()
    {
        Assert.DoesNotContain("Date", Enum.GetNames<ClockStyle>());
        Assert.DoesNotContain("Date", Enum.GetNames<ClockSetting>());
        Assert.Null(typeof(ClockConfig).GetProperty("ShowDate"));

        // A plain clock has no line under its face: its natural box is the face's step alone in the inset.
        var plain = ClockFace.Sizing(new ClockConfig(), string.Empty).Natural;
        Assert.Equal(ClockFace.Step.LineHeight + 24 + LayoutMath.Slack, plain.Height);
    }
}
