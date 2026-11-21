using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-12833] How small a face that scales goes (owner, 2026-11-19: "for the widgets that can scale there should basically be
/// no lower limit except for one that makes sense so you can still see the widget"; "the clock has a min height that is way
/// to big"): its main reading at the legibility floor, its smaller parts giving way on the way down.
/// </summary>
public sealed class FaceFloorTests
{
    /// <summary>A face of a 68 reading 175 wide (its ink 48), a 28 unit after it (55 wide) and a 14 line under it (16 more ink).</summary>
    private static readonly double[] Sizes = [68, 28, 14];

    private static FacePlan Plan(double floor)
        => new(175 + (floor <= 28 ? 55 : 0), 48 + (floor <= 14 ? 16 : 0));

    [Fact]
    public void S12833_the_main_reading_alone_sets_the_least_box_at_the_floor()
    {
        var least = FaceFloor.LeastBox(Sizes, Plan);

        // Not the line under's floor (9 / 14) nor the unit's (9 / 28): the reading's, 9 / 68.
        Assert.Equal(175 * 9 / 68.0, least.Width, 6);
        Assert.Equal(48 * 9 / 68.0, least.Height, 6);
    }

    [Fact]
    public void S12833_at_its_natural_room_every_part_is_drawn_and_nothing_caps_the_face()
    {
        var fit = BlockLayout.Decide(230, 64, Sizes, Plan);

        // Nothing gave way: everything is drawn, a piece the face did not state too.
        Assert.Equal(new FaceFit(0, 1), fit);
        Assert.True(fit.Draws(14) && fit.Draws(28) && fit.Draws(68));
        Assert.Equal(FaceFit.All, BlockLayout.Decide(0, 64, Sizes, Plan));
        Assert.Equal(FaceFit.All, BlockLayout.Decide(double.NaN, 64, Sizes, Plan));
    }

    [Fact]
    public void S12833_the_smallest_part_gives_way_first_and_no_drawn_text_is_under_the_floor()
    {
        // Half the room: the line under would be 7, so it goes; the unit stays at 14.
        var half = BlockLayout.Decide(115, 32, Sizes, Plan);
        Assert.Equal(28, half.Floor);
        Assert.False(half.Draws(14));

        // [S-14318] A short room: the line under goes and the reading and its unit grow back into the room it left (WR-1,
        // WR-4), a one-line face too: held at the scale it went at (9 / 14) the face filled neither axis.
        var shorter = BlockLayout.Decide(230, 40, Sizes, Plan);
        Assert.Equal(28, shorter.Floor);
        Assert.Equal(40 / 48.0, shorter.Scale, 6);

        // A fifth: the unit would be under 9 too; the reading alone, at what the room gives it.
        var fifth = BlockLayout.Decide(46, 13, Sizes, Plan);
        Assert.Equal(68, fifth.Floor);
        Assert.InRange(fifth.Scale * 68, ScaleFloor.Text, 68);
    }

    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(1.0, 0.0)]
    [InlineData(0.0, 1.0)]
    public void S12833_S14318_the_parts_only_fall_as_the_room_shrinks_and_the_rest_fills_it(double byWidth, double byHeight)
    {
        // The room shrinks from the natural box towards nothing: proportionally, by its width alone or by its height
        // alone (the other axis kept). A part that went never comes back; what is left fills the room on one axis.
        double lastScale = double.PositiveInfinity, lastFloor = 0;
        for (var t = 1.5; t >= 0.02; t -= 0.01)
        {
            var width = 230 * (byWidth > 0 ? t : 1.5);
            var height = 64 * (byHeight > 0 ? t : 1.5);
            var fit = BlockLayout.Decide(width, height, Sizes, Plan);

            Assert.True(fit.Floor >= lastFloor - 1e-9, $"t {t:0.00}: a part came back");
            Assert.Equal(Math.Max(ScaleFloor.Least(68), BlockLayout.Scale(width, height, Plan(fit.Floor))), fit.Scale, 9);
            foreach (var size in Sizes.Where(fit.Draws))
            {
                Assert.True(size * fit.Scale >= ScaleFloor.Text - 1e-6, $"t {t:0.00}: {size} drawn at {size * fit.Scale}");
            }

            lastScale = fit.Scale;
            lastFloor = fit.Floor;
        }

        // Down to the reading alone at the floor.
        Assert.Equal(68, lastFloor);
        Assert.Equal(ScaleFloor.Least(68), lastScale, 6);
    }

    [Fact]
    public void S12833_a_face_scales_down_to_its_least_box_never_under_the_least_widget_nor_over_its_stated_min()
    {
        var sizing = WidgetSizing.Range(new(250, 90), new(250, 90), new(500, 90)).ScalesDownTo(new(47.2, 30.3));
        Assert.Equal(new SizeDip(48, WidgetSizing.LeastScaled), sizing.Min);
        Assert.Equal(new SizeDip(250, 90), sizing.Natural);

        var stated = WidgetSizing.Range(new(40, 40), new(250, 90), new(500, 90)).ScalesDownTo(new(120, 60));
        Assert.Equal(new SizeDip(40, 40), stated.Min);

    }

    [Fact]
    public void S12833_a_block_fills_its_room_and_is_never_drawn_under_its_least()
    {
        var free = BlockFit.Lay(468, 104, 156, 52, least: 0.1);
        Assert.Equal(2, free.Scale, 6);

        // Never under its least.
        Assert.Equal(0.3, BlockFit.Lay(10, 10, 156, 52, least: 0.3).Scale, 6);
        Assert.Equal(0.3, BlockLayout.Fill(10, 10, 156, 52, new LineInk(9.2, 4), least: 0.3).Scale, 6);
    }

    [Fact]
    public void S12833_a_readouts_parts_are_its_value_and_the_pieces_that_give_way()
    {
        // A reading on 28 led by its glyph, its unit after it and its line under: the value is the main reading.
        var step = FamilyType.Big28;
        var face = FaceParts.Readout(step, new ReadoutText("100", "%", Lead: true, LineUnder: true), underWidth: 120);

        Assert.Equal(28, face.Main);
        Assert.Equal(new double[] { 28, ScaleFloor.AsText(28), FamilyType.Unit(step).FontSize, FamilyType.Under(step).FontSize }, face.Sizes);
        Assert.Equal(21, ScaleFloor.AsText(28), 6);

        var all = face.Plan(0);
        var value = HeroFit.Ems("100") * 28;
        Assert.Equal(Math.Max(28 + FamilySpace.GlyphGap + value + (HeroFit.Ems("%") * 14), 120), all.Width, 6);
        Assert.Equal(36 + 16 - ((36 - 28) / 2.0) - LineInk.Of(FamilyType.Caption).Under, all.Ink, 6);

        // The line under goes first (12), then the unit (14), then the lead (21): the value alone, its capitals.
        var alone = face.Plan(28);
        Assert.Equal(value, alone.Width, 6);
        Assert.Equal(28 * LineInk.FaceCap, alone.Ink, 6);
        var least = face.LeastBox();
        Assert.Equal(value * 9 / 28, least.Width, 6);
        Assert.Equal(28 * LineInk.FaceCap * 9 / 28, least.Height, 6);
    }

    [Fact]
    public void S12833_a_controls_row_gives_way_at_body_and_a_row_with_nothing_left_takes_no_room()
    {
        var face = FaceParts.Readout(FamilyType.Big28, new ReadoutText("100", "%")).Then(ControlsRules.Row(160));

        Assert.Contains(ControlsRules.GivesWayAt, face.Sizes);
        var with = face.Plan(0);
        var without = face.Plan(FamilyType.Unit(FamilyType.Big28).FontSize + 1);
        Assert.Equal(160, with.Width);
        Assert.Equal(with.Ink - FamilySpace.S2 - FamilyShape.Button - LineInk.Of(FamilyType.Big28).Under, without.Ink, 6);
        Assert.True(face.Keeps(0, 100) && face.Keeps(28, 100) && !face.Keeps(14, 15));
    }

    [Fact]
    public void S12833_S14318_a_stated_faces_parts_only_go_as_its_room_shrinks()
    {
        var face = FaceParts.Readout(FamilyType.Big28, new ReadoutText("100", "%", Lead: true, LineUnder: true), 140).Then(ControlsRules.Row(176));
        var natural = face.Plan(0);
        double lastFloor = 0;
        for (var t = 1.0; t >= 0.05; t -= 0.01)
        {
            var fit = face.Fit(natural.Width * t, natural.Ink * t);
            Assert.True(fit.Floor >= lastFloor);
            Assert.All(face.Sizes.Where(fit.Draws), size => Assert.True(size * fit.Scale >= ScaleFloor.Text - 1e-6));
            lastFloor = fit.Floor;
        }

        Assert.Equal(28, lastFloor);
    }

    /// <summary>A countdown's face: its name (14) over its time (68, <paramref name="time"/> wide) over its date (14).</summary>
    private static FaceParts Countdown(double time) => new(68,
    [
        new FaceRow([FacePiece.Text(FamilyType.BodyStrong, 50, 14)]),
        new FaceRow([FacePiece.Text(FamilyType.Hero68, time)], GapOver: FamilySpace.S1),
        new FaceRow([FacePiece.Text(FamilyType.Body, 110, 14)]),
    ]);

    [Fact]
    public void S14243_pieces_give_way_by_the_block_as_drawn_not_by_its_widest_reading()
    {
        // The countdown's 300 x 164 card (its content 276 x 140): planned on its widest wording (436 wide) the name and the
        // date would be drawn at 8.9 and gave way, while the "3d 5h" it shows (171 wide) draws them at 15 (owner, WR-7:
        // "Keep them together"). Its 330 x 140 neighbour (306 x 116) kept all three either way.
        Assert.Equal(68, Countdown(436).Fit(276, 140).Floor);
        Assert.Equal(0, Countdown(171).Fit(276, 140).Floor);
        Assert.Equal(0, Countdown(436).Fit(306, 116).Floor);

        // Drawn, every kept piece stays at the floor or over it; in its least card the time stands alone.
        var drawn = Countdown(171).Fit(276, 140);
        Assert.True(14 * drawn.Scale >= ScaleFloor.Text);
        Assert.Equal(68, Countdown(171).Fit(58, 20).Floor);
    }

    /// <summary>
    /// [S-14790] Parts of one size give way one at a time in their kind's order (owner, 2026-11-20: "the next meeting clips
    /// the time into the title"; a meeting's three lines, all one size, went together and left its title alone): stated a
    /// hair under that size (<see cref="FaceFloor.Before"/>), the first in the order goes first, and each is still judged by
    /// its own size.
    /// </summary>
    [Fact]
    public void S14790_parts_of_one_size_give_way_one_at_a_time_in_their_order()
    {
        // A 20 title (ink 20) over three lines of 12, 24 each: the time (last to go), the place, the organiser (first).
        var time = 12.0;
        var place = FaceFloor.Before(time, 1);
        var organizer = FaceFloor.Before(time, 2);
        Assert.True(organizer < place && place < time && time - organizer < 1e-5);
        double[] sizes = [20, time, place, organizer];
        FacePlan Plan(double floor) => new(200, 20 + (24 * new[] { time, place, organizer }.Count(s => s >= floor - 1e-9)));

        // All three over 0.75 (12 over 9): the ink 92 fits a 70 room.
        Assert.Equal(0, BlockLayout.Decide(400, 70, sizes, Plan).Floor);

        // At exactly 0.75 the organiser, a hair under 12, is judged by its 12: drawn at 9, it stays (the order's step
        // dropped it from the owner's 370 x 90, the block filling its room at exactly that scale).
        Assert.Equal(0, BlockLayout.Decide(400, 69, sizes, Plan).Floor);

        // A little shorter: the organiser alone goes (the two left fit at 0.75 in 51), never the three at once.
        var one = BlockLayout.Decide(400, 60, sizes, Plan);
        Assert.Equal(place, one.Floor);
        Assert.False(one.Draws(organizer));
        Assert.True(one.Draws(place) && one.Draws(time));

        // Shorter again: the place goes too, the time stays.
        var two = BlockLayout.Decide(400, 46, sizes, Plan);
        Assert.Equal(time, two.Floor);
        Assert.True(two.Draws(time));

        // Each is judged by its own size: none is drawn under the floor.
        foreach (var room in new[] { 100.0, 70, 69, 60, 51, 46, 33, 20 })
        {
            var fit = BlockLayout.Decide(400, room, sizes, Plan);
            Assert.All(sizes.Where(fit.Draws), size => Assert.True(size * fit.Scale >= ScaleFloor.Text - 1e-4, $"{size} at {room}"));
            Assert.True(!fit.Draws(place) || fit.Draws(time));
            Assert.True(!fit.Draws(organizer) || fit.Draws(place));
        }

        // Nothing before: the size itself.
        Assert.Equal(14, FaceFloor.Before(14, 0));
        Assert.Equal(14, FaceFloor.Before(14, -2));
    }
}
