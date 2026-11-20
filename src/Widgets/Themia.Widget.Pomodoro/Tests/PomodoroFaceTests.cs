using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Pomodoro.Tests;

/// <summary>[S-1466] [S-8117] The pomodoro's face: its looks, its two arrangements, the one the card's aspect picks and the card's sizing.</summary>
public sealed class PomodoroFaceTests
{
    private static PomodoroConfig Of(PomodoroStyle style) => new() { Style = style };

    private static PomodoroPlan Plan(PomodoroStyle style) => PomodoroFace.Plan(Of(style), Locale.En);

    [Fact]
    public void S1466_the_time_is_sized_on_the_longest_phases_widest_time()
    {
        Assert.Equal("88:88", PomodoroFace.Worst(new PomodoroConfig()).Value);
        Assert.Equal("888:88", PomodoroFace.Worst(new PomodoroConfig { Phases = [PomodoroPhaseSpec.Of(PomodoroPhaseKind.Focus), PomodoroPhaseSpec.Of(PomodoroPhaseKind.LongBreak, 120)] }).Value);
        Assert.True(PomodoroFace.Worst(new PomodoroConfig()).OneReadout);
    }

    [Fact]
    public void S8116_the_caption_is_sized_on_the_cycles_longest()
        => Assert.Equal("Short break · 2 of 8", PomodoroFace.WorstCaption(new PomodoroConfig(), Locale.En));

    [Fact]
    public void S8117_the_ring_style_sets_the_time_on_40_in_its_ring_the_bar_and_digits_on_68_stacked_and_96_side_by_side()
    {
        var ring = Plan(PomodoroStyle.Ring);
        Assert.Equal(PomodoroLook.Ring, ring.Look);
        Assert.Equal(FamilyType.Big40, ring.Step);
        Assert.True(ring.Ring >= FamilyShape.IconHuge);
        Assert.Equal(0, ring.Ring % 4);

        Assert.Equal(FamilyType.Big40, ring.WideStep);

        Assert.Equal(PomodoroLook.Bar, Plan(PomodoroStyle.Bar).Look);
        Assert.Equal(FamilyType.Hero68, Plan(PomodoroStyle.Bar).StepIn(PomodoroArrangement.Tall));
        Assert.Equal(FamilyType.Hero96, Plan(PomodoroStyle.Bar).StepIn(PomodoroArrangement.Wide));
        Assert.Equal(PomodoroLook.Digits, Plan(PomodoroStyle.Digits).Look);
        Assert.Equal(0, Plan(PomodoroStyle.Digits).Ring);
    }

    [Theory]
    [InlineData(PomodoroStyle.Ring)]
    [InlineData(PomodoroStyle.Bar)]
    [InlineData(PomodoroStyle.Digits)]
    public void S8117_side_by_side_is_wider_and_shorter_than_stacked(PomodoroStyle style)
    {
        var plan = Plan(style);

        Assert.True(plan.Wide.Width > plan.Tall.Width);
        Assert.True(plan.Wide.Height < plan.Tall.Height);
        Assert.True(plan.Wide.Width / plan.Wide.Height > 2 * (plan.Tall.Width / plan.Tall.Height));
    }

    [Theory]
    [InlineData(PomodoroStyle.Bar)]
    [InlineData(PomodoroStyle.Digits)]
    public void S8117_side_by_side_the_band_is_the_taller_of_the_times_capitals_and_the_column(PomodoroStyle style)
    {
        var plan = Plan(style);
        var caps = FamilyType.Hero96.LineHeight - LineInk.Of(FamilyType.Hero96).Over - LineInk.Of(FamilyType.Hero96).Under;
        var column = FamilyType.BodyLine + (style == PomodoroStyle.Bar ? FamilySpace.S2 + FamilyShape.Track : 0) + FamilySpace.S2 + FamilyShape.Button - LineInk.Of(FamilyType.Body).Over;

        Assert.Equal(Math.Max(caps, column), plan.Wide.Height, 6);
    }

    [Fact]
    public void S8117_stacked_the_bar_takes_its_8_and_its_track_under_the_caption()
        => Assert.Equal(FamilySpace.S2 + FamilyShape.Track, Plan(PomodoroStyle.Bar).Tall.Height - Plan(PomodoroStyle.Digits).Tall.Height, 6);

    [Fact]
    public void S8117_stacked_the_controls_are_the_widest_part_and_side_by_side_the_face_and_the_column_stand_24_apart()
    {
        var plan = Plan(PomodoroStyle.Digits);
        var controls = PomodoroFace.ControlsWidth(Locale.En);

        Assert.Equal(controls, plan.Tall.Width, 6);
        Assert.Equal(ReadoutRules.Width(FamilyType.Hero96, PomodoroFace.Worst(new PomodoroConfig())) + PomodoroFace.ColumnGap + controls, plan.Wide.Width, 6);
    }

    [Theory]
    [InlineData(PomodoroStyle.Ring)]
    [InlineData(PomodoroStyle.Bar)]
    [InlineData(PomodoroStyle.Digits)]
    public void S8117_the_cards_aspect_picks_the_arrangement(PomodoroStyle style)
    {
        var plan = Plan(style);

        Assert.Equal(PomodoroArrangement.Wide, PomodoroFace.Choose(plan.Wide.Width, plan.Wide.Height, plan));
        Assert.Equal(PomodoroArrangement.Tall, PomodoroFace.Choose(plan.Tall.Width, plan.Tall.Height, plan));
        Assert.Equal(PomodoroArrangement.Wide, PomodoroFace.Choose(plan.Wide.Width * 3, plan.Wide.Height * 2, plan));
        Assert.Equal(PomodoroArrangement.Tall, PomodoroFace.Choose(plan.Tall.Width * 2, plan.Tall.Height * 3, plan));
    }

    [Fact]
    public void S8117_a_wide_room_too_small_to_read_side_by_side_stacks()
    {
        var plan = Plan(PomodoroStyle.Bar);
        var threshold = Math.Sqrt(plan.Tall.Width / plan.Tall.Height * (plan.Wide.Width / plan.Wide.Height));
        var (w, h) = (plan.Tall.Width, plan.Tall.Width / threshold);

        Assert.True(PomodoroFace.Scale(w, h, plan, PomodoroArrangement.Wide) < PomodoroFace.Least);
        Assert.Equal(PomodoroArrangement.Tall, PomodoroFace.Choose(w, h, plan));
    }

    [Fact]
    public void S8117_an_unmeasured_room_stands_side_by_side_as_a_new_card_does()
        => Assert.Equal(PomodoroArrangement.Wide, PomodoroFace.Choose(double.NaN, 0, Plan(PomodoroStyle.Ring)));

    [Theory]
    [InlineData(PomodoroStyle.Ring)]
    [InlineData(PomodoroStyle.Bar)]
    [InlineData(PomodoroStyle.Digits)]
    public void S8117_a_new_card_is_side_by_side_the_least_the_stacked_block_at_its_floor(PomodoroStyle style)
    {
        var plan = Plan(style);
        var sizing = PomodoroFace.Sizing(Of(style), Locale.En);
        var inset = (2 * FamilySpace.Inset) + PomodoroFace.Slack;

        Assert.Equal(Math.Ceiling(plan.Tall.Width * PomodoroFace.Least) + inset, sizing.Min.Width);
        Assert.Equal(Math.Ceiling(plan.Tall.Height * PomodoroFace.Least) + inset, sizing.Min.Height);
        Assert.Equal(Math.Ceiling(plan.Wide.Width) + inset, sizing.Natural.Width);
        Assert.True(sizing.Natural.Height >= sizing.Min.Height);
        Assert.Empty(sizing.Problems());
        Assert.Equal(PomodoroArrangement.Wide, PomodoroFace.Choose(sizing.Natural.Width - inset, sizing.Natural.Height - inset, plan));
    }

    [Theory]
    [InlineData(PomodoroStyle.Ring)]
    [InlineData(PomodoroStyle.Bar)]
    [InlineData(PomodoroStyle.Digits)]
    public void S8117_every_card_from_the_least_up_holds_its_arrangement_at_a_scale_that_reads(PomodoroStyle style)
    {
        var plan = Plan(style);
        var sizing = PomodoroFace.Sizing(Of(style), Locale.En);
        var inset = (2 * FamilySpace.Inset) + PomodoroFace.Slack;
        for (var w = sizing.Min.Width; w <= sizing.Min.Width * 4; w += 13)
        {
            for (var h = sizing.Min.Height; h <= sizing.Min.Height * 4; h += 11)
            {
                var (rw, rh) = (w - inset, h - inset);
                Assert.True(PomodoroFace.Scale(rw, rh, plan, PomodoroFace.Choose(rw, rh, plan)) >= PomodoroFace.Least - 1e-9, $"{w}x{h}");
            }
        }
    }

    [Fact]
    public void S8117_the_face_scales_down_to_its_body_text_at_9_and_its_glyphs_at_12()
        => Assert.Equal(ScaleFloor.Least(FamilyType.Body.FontSize, FamilyShape.Icon), PomodoroFace.Least);
}
