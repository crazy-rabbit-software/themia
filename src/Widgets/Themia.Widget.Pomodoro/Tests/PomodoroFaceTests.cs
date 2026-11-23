using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Pomodoro.Tests;

/// <summary>
/// [S-1466] [S-8117] [S-16600]..[S-16606] The pomodoro's face: the time's step, the ring with everything in it, the
/// arrangements the card's shape picks, the layout in a room and the card's sizing.
/// </summary>
public sealed class PomodoroFaceTests
{
    private static PomodoroConfig Of(PomodoroStyle style) => new() { Style = style };

    private static PomodoroPlan Plan(PomodoroStyle style) => PomodoroFace.Plan(Of(style), Locale.En);

    private static PomodoroLook Look(PomodoroStyle style) => Plan(style).Look;

    private static PomodoroParts Parts => PomodoroFace.Estimate(new PomodoroConfig(), Locale.En);

    private const double Inset = (2 * FamilySpace.Inset) + LayoutMath.Slack;

    private const double Hair = 1e-6;

    /// <summary>Rooms from tall and narrow to wide and short, small to large.</summary>
    public static TheoryData<PomodoroStyle, double, double> Rooms()
    {
        var data = new TheoryData<PomodoroStyle, double, double>();
        foreach (var style in new[] { PomodoroStyle.Ring, PomodoroStyle.Bar, PomodoroStyle.Digits })
        {
            foreach (var (w, h) in new (double, double)[] { (160, 160), (200, 420), (300, 300), (330, 140), (600, 140), (900, 300), (520, 600), (260, 900), (1200, 120) })
            {
                data.Add(style, w, h);
            }
        }

        return data;
    }

    private static IEnumerable<PomodoroRect> Shown(PomodoroLayout at)
        => new[] { at.Ring, at.Time, at.Caption, at.Bar, at.Controls }.Where(r => !r.IsEmpty);

    // ---- the time ([S-16600]) ---------------------------------------------------------------------------------------

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

    [Theory]
    [InlineData(PomodoroStyle.Ring)]
    [InlineData(PomodoroStyle.Bar)]
    [InlineData(PomodoroStyle.Digits)]
    public void S16600_every_style_sets_the_time_on_40_at_most_three_times_the_body_text(PomodoroStyle style)
    {
        // Owner, 2026-11-23: "the pomodoro timer should generally show the time smaller". It was 68 stacked and 96 side by side.
        var plan = Plan(style);
        Assert.Equal(FamilyType.Big40, plan.Step);
        Assert.True(plan.Step.FontSize <= PomodoroFace.TimeRatio * FamilyType.Body.FontSize);
        Assert.True(plan.Step.FontSize < FamilyType.Hero68.FontSize);
    }

    [Theory]
    [InlineData(PomodoroStyle.Ring)]
    [InlineData(PomodoroStyle.Bar)]
    [InlineData(PomodoroStyle.Digits)]
    public void S16600_the_times_figures_stand_no_taller_than_the_caption_over_the_controls(PomodoroStyle style)
    {
        // The time never dominates: its ink is at most as tall as the column of words and buttons it heads.
        var parts = Parts;
        var time = parts.Time.Height - parts.TimeInk.Over - parts.TimeInk.Under;
        var column = parts.Caption.Height - parts.CaptionInk.Over + FamilySpace.S2 + parts.Controls.Height;
        Assert.True(time <= column, $"{style}: time {time} over a column of {column}");

        // In the ring, under a sixth of its diameter (it filled the ring's hole before).
        if (style == PomodoroStyle.Ring)
        {
            Assert.True(time < Plan(style).Ring / 6, $"time {time} in a ring of {Plan(style).Ring}");
        }
    }

    // ---- the ring ([S-16601], [S-16602]) -------------------------------------------------------------------------------

    [Fact]
    public void S16602_the_circle_holds_every_rows_corners_touches_the_farthest_and_centres_the_rows_down()
    {
        var rows = PomodoroFace.RingRows(Parts);
        var (radius, top) = PomodoroFace.Circle(rows);
        var shift = top - rows.Min(r => r.Top);
        var far = 0.0;
        foreach (var row in rows)
        {
            foreach (var y in new[] { row.Top + shift, row.Bottom + shift })
            {
                var d = Math.Sqrt((row.Width / 2 * (row.Width / 2)) + (y * y));
                Assert.True(d <= radius + Hair, $"{row} at {d} of {radius}");
                far = Math.Max(far, d);
            }
        }

        Assert.Equal(radius, far, 6);

        // A dial: as much room over the time's figures as under the controls.
        Assert.Equal(-(rows.Max(r => r.Bottom) + shift), rows.Min(r => r.Top) + shift, 6);

        // The time first, the caption under it, the controls last, each under the one before.
        Assert.True(rows[0].Bottom <= rows[1].Top && rows[1].Bottom <= rows[2].Top);
    }

    [Fact]
    public void S16602_the_ring_is_the_least_in_fours_whose_hole_holds_the_parts_with_its_pad()
    {
        var plan = Plan(PomodoroStyle.Ring);
        var (radius, _) = PomodoroFace.Circle(PomodoroFace.RingRows(plan.Parts));
        Assert.Equal(0, plan.Ring % 4);
        Assert.True((plan.Ring / 2) - FamilyShape.Stroke - PomodoroFace.RingPad >= radius - Hair);
        Assert.True((plan.Ring - 4) / 2 - FamilyShape.Stroke - PomodoroFace.RingPad < radius);

        // Far smaller than the ring round the inner square would be (the controls' width its side).
        Assert.True(plan.Ring < Math.Sqrt(2) * plan.Parts.Controls.Width);
    }

    [Theory]
    [MemberData(nameof(Rooms))]
    public void S16601_the_ring_is_as_large_as_the_rooms_smaller_side_and_holds_every_part(PomodoroStyle style, double w, double h)
    {
        if (style != PomodoroStyle.Ring)
        {
            return;
        }

        var at = PomodoroFace.Lay(PomodoroLook.Ring, Parts, w, h);
        Assert.Equal(PomodoroArrangement.Ring, at.Arrangement);
        Assert.Equal(Math.Min(w, h), at.Ring.Width, 6);
        Assert.Equal(at.Ring.Width, at.Ring.Height, 6);
        Assert.Equal((w - at.Ring.Width) / 2, at.Ring.X, 6);
        Assert.Equal((h - at.Ring.Height) / 2, at.Ring.Y, 6);
        Assert.True(at.Bar.IsEmpty);

        // Every part's ink inside the ring's hole, less its pad, at the scale: the time's figures, the caption, the controls.
        var (cx, cy) = (at.Ring.X + (at.Ring.Width / 2), at.Ring.Y + (at.Ring.Height / 2));
        var hole = (at.Ring.Width / 2) - ((FamilyShape.Stroke + PomodoroFace.RingPad) * at.Scale);
        var parts = Parts;
        var s = at.Scale;
        var inks = new[]
        {
            new PomodoroRect(at.Time.X, at.Time.Y + (parts.TimeInk.Over * s), at.Time.Width, at.Time.Height - ((parts.TimeInk.Over + parts.TimeInk.Under) * s)),
            new PomodoroRect(at.Caption.X, at.Caption.Y + (parts.CaptionInk.Over * s), at.Caption.Width, at.Caption.Height - (parts.CaptionInk.Over * s)),
            at.Controls,
        };
        foreach (var ink in inks)
        {
            foreach (var (x, y) in new[] { (ink.X, ink.Y), (ink.Right, ink.Y), (ink.X, ink.Bottom), (ink.Right, ink.Bottom) })
            {
                var d = Math.Sqrt(((x - cx) * (x - cx)) + ((y - cy) * (y - cy)));
                Assert.True(d <= hole + 1e-6, $"{w}x{h}: corner {x:F1},{y:F1} at {d:F2} of the hole's {hole:F2}");
            }
        }
    }

    [Fact]
    public void S16601_a_regression_the_caption_and_the_controls_stood_beside_or_under_the_ring()
    {
        // Before (owner, 2026-11-23: "the version that has the circle should show everything inside of the circle, not
        // outside too"): at 330x140 the caption and the controls stood beside the ring, at 220x400 under it.
        foreach (var (w, h) in new (double, double)[] { (330, 140), (220, 400) })
        {
            var at = PomodoroFace.Lay(PomodoroLook.Ring, Parts, w, h);
            foreach (var part in new[] { at.Time, at.Caption, at.Controls })
            {
                Assert.True(part.X >= at.Ring.X && part.Right <= at.Ring.Right && part.Y >= at.Ring.Y && part.Bottom <= at.Ring.Bottom, $"{w}x{h}: {part} outside {at.Ring}");
            }
        }
    }

    // ---- the arrangements ([S-16603], [S-16604], [S-16605]) ------------------------------------------------------------

    [Theory]
    [InlineData(PomodoroStyle.Bar)]
    [InlineData(PomodoroStyle.Digits)]
    public void S16603_side_by_side_is_wider_and_shorter_than_stacked(PomodoroStyle style)
    {
        var look = Look(style);
        var (stacked, side) = (PomodoroFace.BlockOf(PomodoroArrangement.Stacked, look, Parts), PomodoroFace.BlockOf(PomodoroArrangement.Side, look, Parts));
        Assert.True(side.Width > stacked.Width);
        Assert.True(side.Height < stacked.Height);
        Assert.True(side.Width / side.Height > 2 * (stacked.Width / stacked.Height));
    }

    [Theory]
    [InlineData(PomodoroStyle.Bar)]
    [InlineData(PomodoroStyle.Digits)]
    public void S16603_the_cards_aspect_picks_stacked_or_side_by_side(PomodoroStyle style)
    {
        var look = Look(style);
        var (stacked, side) = (PomodoroFace.BlockOf(PomodoroArrangement.Stacked, look, Parts), PomodoroFace.BlockOf(PomodoroArrangement.Side, look, Parts));
        Assert.Equal(PomodoroArrangement.Side, PomodoroFace.Choose(look, Parts, side.Width, side.Height));
        Assert.Equal(PomodoroArrangement.Stacked, PomodoroFace.Choose(look, Parts, stacked.Width, stacked.Height));
        Assert.Equal(PomodoroArrangement.Side, PomodoroFace.Choose(look, Parts, side.Width * 3, side.Height * 2));
        Assert.Equal(PomodoroArrangement.Stacked, PomodoroFace.Choose(look, Parts, stacked.Width * 3, stacked.Height * 3));
    }

    [Theory]
    [InlineData(200, 420)]
    [InlineData(260, 900)]
    [InlineData(300, 301)]
    public void S16604_the_bar_style_in_a_card_taller_than_wide_stands_upright_beside_its_bar(double w, double h)
    {
        var at = PomodoroFace.Lay(PomodoroLook.Bar, Parts, w, h);
        Assert.Equal(PomodoroArrangement.Column, at.Arrangement);
        Assert.True(at.BarUpright);

        // The bar runs the room's whole height on its left; the parts stand beside it, on their start.
        Assert.Equal(0, at.Bar.Y, 6);
        Assert.Equal(h, at.Bar.Height, 6);
        Assert.Equal(FamilyShape.Track * at.Scale, at.Bar.Width, 6);
        Assert.Equal(at.Bar.Right + (PomodoroFace.BarGap * at.Scale), at.Time.X, 6);
        Assert.Equal(at.Time.X, at.Caption.X, 6);
        Assert.Equal(at.Time.X, at.Controls.X, 6);
    }

    [Theory]
    [InlineData(300, 300)]
    [InlineData(330, 140)]
    [InlineData(600, 140)]
    [InlineData(1200, 120)]
    public void S16604_in_a_card_as_wide_as_tall_or_wider_the_bar_runs_its_whole_width(double w, double h)
    {
        var at = PomodoroFace.Lay(PomodoroLook.Bar, Parts, w, h);
        Assert.NotEqual(PomodoroArrangement.Column, at.Arrangement);
        Assert.Equal(0, at.Bar.X, 6);
        Assert.Equal(w, at.Bar.Width, 6);
        Assert.Equal(FamilyShape.Track * at.Scale, at.Bar.Height, 6);

        // Under the caption (stacked) or under the time and the column both (side by side), 8 at the scale.
        var over = at.Arrangement == PomodoroArrangement.Side ? Math.Max(at.Controls.Bottom, at.Time.Bottom - (Parts.TimeInk.Under * at.Scale)) : at.Caption.Bottom;
        Assert.True(at.Bar.Y >= over - 1e-6, $"{w}x{h}: bar at {at.Bar.Y} over {over}");
    }

    [Fact]
    public void S16604_a_tall_room_too_narrow_to_read_beside_the_bar_stacks()
    {
        var look = PomodoroLook.Bar;
        var column = PomodoroFace.BlockOf(PomodoroArrangement.Column, look, Parts);
        var (w, h) = (column.Width * (PomodoroFace.Least - 0.02), 900);
        Assert.True(PomodoroFace.Scale(PomodoroArrangement.Column, look, Parts, w, h) < PomodoroFace.Least);
        Assert.Equal(PomodoroArrangement.Stacked, PomodoroFace.Choose(look, Parts, w, h));
    }

    [Theory]
    [InlineData(PomodoroStyle.Bar)]
    [InlineData(PomodoroStyle.Digits)]
    public void S16603_an_unmeasured_room_stands_side_by_side_as_a_new_card_does(PomodoroStyle style)
        => Assert.Equal(PomodoroArrangement.Side, PomodoroFace.Choose(Look(style), Parts, double.NaN, 0));

    [Theory]
    [InlineData(PomodoroStyle.Bar)]
    [InlineData(PomodoroStyle.Digits)]
    public void S14281_side_by_side_the_columns_parts_touch_and_stand_centred_on_the_times_band(PomodoroStyle style)
    {
        // WR-7: "Keep them together". The caption and the controls 8 under it, never spread over the band.
        var at = PomodoroFace.Lay(Look(style), Parts, 600, 100);
        Assert.Equal(PomodoroArrangement.Side, at.Arrangement);
        var s = at.Scale;
        Assert.Equal(at.Caption.Bottom + (FamilySpace.S2 * s), at.Controls.Y, 6);
        Assert.Equal(at.Time.Right + (PomodoroFace.ColumnGap * s), at.Caption.X, 6);

        // The time's figures and the column's ink (the caption's capitals to the controls' foot) share one middle.
        var time = (at.Time.Y + (Parts.TimeInk.Over * s) + at.Time.Bottom - (Parts.TimeInk.Under * s)) / 2;
        var column = (at.Caption.Y + (Parts.CaptionInk.Over * s) + at.Controls.Bottom) / 2;
        Assert.Equal(time, column, 6);
    }

    [Fact]
    public void S16605_digits_never_stands_upright_and_draws_no_bar()
    {
        Assert.DoesNotContain(PomodoroArrangement.Column, PomodoroFace.Arrangements(PomodoroLook.Digits));
        var at = PomodoroFace.Lay(PomodoroLook.Digits, Parts, 200, 600);
        Assert.Equal(PomodoroArrangement.Stacked, at.Arrangement);
        Assert.True(at.Bar.IsEmpty);
    }

    [Theory]
    [MemberData(nameof(Rooms))]
    public void S16603_the_parts_scale_as_one_fill_one_axis_and_stand_centred_on_the_other(PomodoroStyle style, double w, double h)
    {
        var look = Look(style);
        var at = PomodoroFace.Lay(look, Parts, w, h);
        var block = PomodoroFace.BlockOf(at.Arrangement, look, Parts);
        Assert.Equal(Math.Min(w / block.Width, h / block.Height), at.Scale, 6);

        // Every part's box in the room, its size its natural one at the one scale.
        Assert.Equal(Parts.Time.Width * at.Scale, at.Time.Width, 6);
        Assert.Equal(Parts.Caption.Width * at.Scale, at.Caption.Width, 6);
        Assert.Equal(Parts.Controls.Width * at.Scale, at.Controls.Width, 6);
        // Every part's ink in the room (a text's box may reach over it by the room over its capitals): the block fills one
        // axis to both its edges and is centred on the other.
        var timeInk = new PomodoroRect(at.Time.X, at.Time.Y + (Parts.TimeInk.Over * at.Scale), at.Time.Width, at.Time.Height - ((Parts.TimeInk.Over + Parts.TimeInk.Under) * at.Scale));
        var captionInk = at.Caption with { Y = at.Caption.Y + (Parts.CaptionInk.Over * at.Scale), Height = at.Caption.Height - (Parts.CaptionInk.Over * at.Scale) };
        var inks = new[] { at.Ring, timeInk, captionInk, at.Bar, at.Controls }.Where(r => !r.IsEmpty).ToList();
        foreach (var r in inks)
        {
            Assert.True(r.X >= -1e-6 && r.Right <= w + 1e-6 && r.Y >= -1e-6 && r.Bottom <= h + 1e-6, $"{style} {w}x{h}: {r}");
        }

        var (left, right, top, foot) = (inks.Min(r => r.X), w - inks.Max(r => r.Right), inks.Min(r => r.Y), h - inks.Max(r => r.Bottom));
        var across = left < 0.5 && right < 0.5;
        var down = top < 0.5 && foot < 0.5;
        Assert.True(across || down, $"{style} {w}x{h} {at.Arrangement}: bands {left:F1}/{right:F1} x {top:F1}/{foot:F1}");
        if (!across)
        {
            Assert.Equal(left, right, 3);
        }

        if (!down)
        {
            Assert.Equal(top, foot, 3);
        }
    }

    [Theory]
    [MemberData(nameof(Rooms))]
    public void S16603_no_two_parts_overlap(PomodoroStyle style, double w, double h)
    {
        var at = PomodoroFace.Lay(Look(style), Parts, w, h);
        var boxes = new[] { at.Time, at.Caption, at.Bar, at.Controls }.Where(r => !r.IsEmpty).ToList();
        for (var i = 0; i < boxes.Count; i++)
        {
            for (var j = i + 1; j < boxes.Count; j++)
            {
                var (a, b) = (boxes[i], boxes[j]);
                var overlap = Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X) > 1e-6 && Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y) > 1e-6;
                Assert.False(overlap, $"{style} {w}x{h}: {a} and {b}");
            }
        }
    }

    [Theory]
    [InlineData(PomodoroStyle.Bar)]
    [InlineData(PomodoroStyle.Digits)]
    public void S16603_each_arrangement_keeps_its_parts_where_they_stand_across_its_range(PomodoroStyle style)
    {
        // Owner, of another widget: "it should scale to the size properly but it should not change its layout drastically":
        // within one arrangement a larger room scales the same layout, every part where it stood relative to the others.
        var look = Look(style);
        var a = PomodoroFace.Lay(look, Parts, 400, 70);
        var b = PomodoroFace.Lay(look, Parts, 800, 140);
        Assert.Equal(a.Arrangement, b.Arrangement);
        Assert.Equal(2 * a.Scale, b.Scale, 6);
        Assert.Equal(2 * (a.Controls.X - a.Time.X), b.Controls.X - b.Time.X, 6);
        Assert.Equal(2 * (a.Controls.Y - a.Caption.Y), b.Controls.Y - b.Caption.Y, 6);
    }

    // ---- the sizing ([S-16606]) -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(PomodoroStyle.Bar)]
    [InlineData(PomodoroStyle.Digits)]
    public void S16606_a_new_card_is_the_side_by_side_block_grown_to_the_least_height_the_least_the_stacked_block_at_its_floor(PomodoroStyle style)
    {
        var look = Look(style);
        var sizing = PomodoroFace.Sizing(Of(style), Locale.En);
        var (stacked, side) = (PomodoroFace.BlockOf(PomodoroArrangement.Stacked, look, Parts), PomodoroFace.BlockOf(PomodoroArrangement.Side, look, Parts));

        Assert.Equal(Math.Max(WidgetSizing.LeastScaled, Math.Ceiling(stacked.Width * PomodoroFace.Least) + Inset), sizing.Min.Width);
        Assert.Equal(Math.Max(WidgetSizing.LeastScaled, Math.Ceiling(stacked.Height * PomodoroFace.Least) + Inset), sizing.Min.Height);
        Assert.Empty(sizing.Problems());

        // The side-by-side block grown as one to the least height: the new card keeps its shape, the block fills it.
        var grow = Math.Max(1, (sizing.Min.Height - Inset) / side.Height);
        Assert.Equal(Math.Ceiling(side.Width * grow) + Inset, sizing.Natural.Width);
        Assert.Equal(Math.Max(sizing.Min.Height, Math.Ceiling(side.Height * grow) + Inset), sizing.Natural.Height);
        var (rw, rh) = (sizing.Natural.Width - Inset, sizing.Natural.Height - Inset);
        Assert.True(rh - (side.Height * PomodoroFace.Scale(PomodoroArrangement.Side, look, Parts, rw, rh)) < 1.5, "a band over and under the block");
        Assert.Equal(PomodoroArrangement.Side, PomodoroFace.Choose(look, Parts, sizing.Natural.Width - Inset, sizing.Natural.Height - Inset));
    }

    [Fact]
    public void S16606_a_new_ring_card_is_its_ring_the_least_its_ring_at_its_floor()
    {
        var plan = Plan(PomodoroStyle.Ring);
        var sizing = PomodoroFace.Sizing(Of(PomodoroStyle.Ring), Locale.En);
        Assert.Equal(plan.Ring + Inset, sizing.Natural.Width);
        Assert.Equal(plan.Ring + Inset, sizing.Natural.Height);
        Assert.Equal(Math.Ceiling(plan.Ring * PomodoroFace.Least) + Inset, sizing.Min.Width);
        Assert.Empty(sizing.Problems());
    }

    [Theory]
    [InlineData(PomodoroStyle.Ring)]
    [InlineData(PomodoroStyle.Bar)]
    [InlineData(PomodoroStyle.Digits)]
    public void S16606_every_card_from_the_least_up_holds_its_arrangement_at_a_scale_that_reads(PomodoroStyle style)
    {
        var look = Look(style);
        var sizing = PomodoroFace.Sizing(Of(style), Locale.En);
        for (var w = sizing.Min.Width; w <= sizing.Min.Width * 5; w += 13)
        {
            for (var h = sizing.Min.Height; h <= sizing.Min.Height * 5; h += 11)
            {
                var at = PomodoroFace.Lay(look, Parts, w - Inset, h - Inset);
                Assert.True(at.Scale >= PomodoroFace.Least - 1e-9, $"{style} {w}x{h} {at.Arrangement} at {at.Scale}");
            }
        }
    }

    [Fact]
    public void S8117_the_face_scales_down_to_its_body_text_at_9_and_its_glyphs_at_12()
        => Assert.Equal(ScaleFloor.Least(FamilyType.Body.FontSize, FamilyShape.Icon), PomodoroFace.Least);
}
