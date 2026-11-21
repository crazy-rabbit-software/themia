using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-12808] The one layout of a one-readout block (<see cref="BlockLayout.Solve(double, double, IReadOnlyList{PartBox}, BlockFace?, ContentAlign, double, bool)"/>):
/// its parts packed, scaled as one until one axis stops it, their ink where the alignment puts it, the smaller pieces giving
/// way in order down to the face's floor (WR-7, WR-14).
/// </summary>
public sealed class BlockLayoutTests
{
    /// <summary>A 28 reading 120 wide (9.2 over its figures, 7.2 under its baseline) over a 12 caption 90 wide (its ink 4 under it).</summary>
    private static readonly PartBox Reading = new(120, 36, new LineInk(9.2, 7.2), Text: 28);

    private static readonly PartBox Caption = new(90, 16, new LineInk(4, 4), Text: 12, GivesWayAt: 12);

    private static readonly BlockFace Face = new(28, [28, 12]);

    [Fact]
    public void S12808_the_parts_stand_packed_and_the_block_fills_the_height_with_its_ink()
    {
        // 600 x 104: the height stops it. The reading's first ink on the top, the caption's last ink on the foot.
        var solved = BlockLayout.Solve(600, 104, [Reading, Caption], Face, ContentAlign.Center);
        var scale = solved.Fill.Scale;
        Assert.Equal(104 / (52 - 9.2 - 4), scale, 6);

        var reading = solved.Places[0]!.Value;
        var caption = solved.Places[1]!.Value;
        Assert.Equal(0, (reading.Y + 9.2) * scale, 6);
        Assert.Equal(104, (caption.Y + 16 - 4) * scale, 6);

        // WR-7: the caption right under the reading, nothing between them.
        Assert.Equal(reading.Y + 36, caption.Y, 6);
    }

    [Fact]
    public void S12808_cross_axis_centring_where_the_width_stops_it_the_height_left_is_shared_over_and_under_its_ink()
    {
        var solved = BlockLayout.Solve(240, 300, [Reading, Caption], Face, ContentAlign.Center);
        var scale = solved.Fill.Scale;
        Assert.Equal(2, scale, 6);

        var over = (solved.Places[0]!.Value.Y + 9.2) * scale;
        var under = 300 - ((solved.Places[1]!.Value.Y + 16 - 4) * scale);
        Assert.Equal(over, under, 6);
        Assert.True(over > 1);
    }

    [Fact]
    public void S14319_on_its_foot_the_height_left_stands_over_its_first_ink_alone()
    {
        var solved = BlockLayout.Solve(240, 300, [Reading, Caption], Face, ContentAlign.Center, foot: true);
        var scale = solved.Fill.Scale;
        Assert.Equal(300, (solved.Places[1]!.Value.Y + 16 - 4) * scale, 6);
    }

    [Theory]
    [InlineData(ContentAlign.Left)]
    [InlineData(ContentAlign.Center)]
    [InlineData(ContentAlign.Right)]
    public void S14301_each_part_stands_its_ink_where_the_alignment_puts_it_and_a_spanning_part_runs_the_room(ContentAlign align)
    {
        // A reading with side bearings of 2 and 3, a bar that spans: held by the height at 2 in a 600 wide room.
        var reading = Reading with { Left = 2, Right = 3 };
        var bar = new PartBox(0.5, 8, Spans: true);
        var solved = BlockLayout.Solve(600, 2 * (36 + 8 - 9.2), [reading, bar], null, align);
        var scale = solved.Fill.Scale;
        Assert.Equal(2, scale, 6);

        var ink = (solved.Places[0]!.Value.X + 2) * scale;
        var inkEnd = ink + (reading.Own * scale);
        var expected = align switch { ContentAlign.Left => 0, ContentAlign.Right => 600 - (115 * 2), _ => (600 - (115 * 2)) / 2.0 };
        Assert.Equal(expected, ink, 6);
        Assert.True(inkEnd <= 600 + 1e-9);

        var spans = solved.Places[1]!.Value;
        Assert.Equal(0, spans.X, 6);
        Assert.Equal(600, spans.Width * scale, 6);
    }

    [Fact]
    public void S14301_a_width_held_block_stands_its_ink_on_both_side_insets()
    {
        var reading = Reading with { Left = 2, Right = 3 };
        var solved = BlockLayout.Solve(230, 400, [reading], null, ContentAlign.Left);
        var scale = solved.Fill.Scale;
        Assert.Equal(230 / 115.0, scale, 6);
        Assert.Equal(0, (solved.Places[0]!.Value.X + 2) * scale, 6);
        Assert.Equal(230, (solved.Places[0]!.Value.X + 2 + 115) * scale, 6);
    }

    [Fact]
    public void S12833_the_face_floor_the_caption_gives_way_before_the_reading_goes_under_9()
    {
        // At half the natural room the caption would be drawn at 6: it gives way and the reading grows back into its room.
        var solved = BlockLayout.Solve(60, 20, [Reading, Caption], Face, ContentAlign.Center);
        Assert.Equal(28, solved.Floor);
        Assert.False(solved.Shown(1));
        Assert.True(solved.Shown(0));
        Assert.Equal(Math.Min(60 / 120.0, 20 / (36 - 9.2 - 7.2)), solved.Fill.Scale, 6);
        Assert.True(solved.Fill.Scale * 28 >= ScaleFloor.Text);

        // At its natural room every part is drawn.
        var natural = BlockLayout.Solve(120, 52 - 9.2 - 4, [Reading, Caption], Face, ContentAlign.Center);
        Assert.Equal(0, natural.Floor);
        Assert.True(natural.Shown(1));
    }

    [Fact]
    public void S12833_WR14_the_block_never_scales_under_its_main_readings_floor()
    {
        var solved = BlockLayout.Solve(10, 4, [Reading, Caption], Face, ContentAlign.Center);
        Assert.Equal(ScaleFloor.Least(28), solved.Fill.Scale, 6);
        Assert.Equal(ScaleFloor.Text, solved.Fill.Scale * 28, 6);

        // Without a stated face its smallest drawn text sets the floor: the caption's 12 at 9.
        var unstated = BlockLayout.Solve(10, 4, [Reading, Caption with { GivesWayAt = 0 }], null, ContentAlign.Center);
        Assert.Equal(ScaleFloor.Least(12), unstated.Fill.Scale, 6);
    }

    [Fact]
    public void S12833_a_glyph_sets_the_floor_at_12()
    {
        var glyph = new PartBox(16, 16, Glyph: 16);
        var solved = BlockLayout.Solve(4, 4, [glyph], null, ContentAlign.Center);
        Assert.Equal(0.75, solved.Fill.Scale, 6);
    }

    [Fact]
    public void S14790_parts_of_one_size_give_way_in_their_kinds_order()
    {
        // Three 14 lines that go one at a time in their kind's order: the time, then the place, then the organiser.
        var time = new PartBox(100, 20, new LineInk(5, 4), Text: 14, GivesWayAt: FaceFloor.Before(14, 2));
        var place = new PartBox(100, 20, new LineInk(5, 4), Text: 14, GivesWayAt: FaceFloor.Before(14, 1));
        var organiser = new PartBox(100, 20, new LineInk(5, 4), Text: 14, GivesWayAt: 14);
        var title = new PartBox(200, 28, new LineInk(6, 6), Text: 20);
        var face = new BlockFace(20, [20, time.GivesWayAt, place.GivesWayAt, organiser.GivesWayAt]);
        PartBox[] parts = [title, time, place, organiser];

        bool[] Shown(double height)
        {
            var solved = BlockLayout.Solve(400, height, parts, face, ContentAlign.Left);
            return [.. Enumerable.Range(0, parts.Length).Select(solved.Shown)];
        }

        var last = new[] { true, true, true, true };
        foreach (var height in new[] { 120.0, 80, 60, 45, 35, 25, 15 })
        {
            var shown = Shown(height);
            Assert.True(shown[0], $"{height}: the title never goes");

            // The order: the time goes first, then the place, then the organiser; nothing comes back in a smaller room.
            for (var i = 1; i < 4; i++)
            {
                Assert.True(!shown[i] || last[i], $"{height}: part {i} came back");
                Assert.True(i == 3 || !shown[i] || shown[i + 1], $"{height}: part {i + 1} went before part {i}");
            }

            last = shown;
        }

        Assert.Equal([true, false, false, false], Shown(15));
    }

    [Fact]
    public void S14243_the_floor_is_decided_on_the_parts_as_drawn_at_each_floor()
    {
        // A piece inside a part (a unit beside a reading) that gives way: the part is narrower without it.
        IReadOnlyList<PartBox> At(double floor) => [new PartBox(floor > 14 ? 100 : 160, 36, new LineInk(9, 7), Text: floor > 14 ? 28 : 14)];
        var face = new BlockFace(28, [28, 14]);

        // 160 wide: the unit kept at 14 would be drawn at 14 * 0.5: it goes, and the reading fills the width alone.
        var solved = BlockLayout.Solve(80, 400, At, face, ContentAlign.Center);
        Assert.Equal(28, solved.Floor);
        Assert.Equal(0.8, solved.Fill.Scale, 6);

        var roomy = BlockLayout.Solve(320, 400, At, face, ContentAlign.Center);
        Assert.Equal(0, roomy.Floor);
        Assert.Equal(2, roomy.Fill.Scale, 6);
    }

    [Fact]
    public void S14791_a_cut_lines_cap_the_block_never_grows_past_where_the_line_stands_whole()
    {
        // A title asking for its least 84 that is 280 wide whole, in a 346 x 56 room: held at 346 / 280, not the height's 2.8.
        var title = new PartBox(84, 28, LineInk.Of(FamilyType.Title), Spans: true, Uncut: 196, Text: 20);
        var solved = BlockLayout.Solve(346, 56, [title], null, ContentAlign.Left);
        Assert.Equal(346 / 280.0, solved.Fill.Scale, 6);

        // Too narrow even at the floor: the floor, the line cut there.
        var narrow = BlockLayout.Solve(100, 56, [title], null, ContentAlign.Left);
        Assert.Equal(ScaleFloor.Least(20), narrow.Fill.Scale, 6);

        // Nothing cut: the height's scale.
        var uncut = BlockLayout.Solve(346, 56, [title with { Uncut = 0 }], null, ContentAlign.Left);
        Assert.Equal(56 / (28 - LineInk.Of(FamilyType.Title).Over - LineInk.Of(FamilyType.Title).Under), uncut.Fill.Scale, 6);
    }

    [Fact]
    public void S12813_a_placeholder_is_placed_by_the_ink_it_draws()
    {
        // "--" inks lower than figures: its first ink, not the capitals the reading would draw, stands on the top.
        var dashes = Reading with { Ink = new LineInk(19, 7.2) };
        var solved = BlockLayout.Solve(600, 104, [dashes, Caption], Face, ContentAlign.Center);
        Assert.Equal(0, (solved.Places[0]!.Value.Y + 19) * solved.Fill.Scale, 6);
        Assert.Equal(104, (solved.Places[1]!.Value.Y + 12) * solved.Fill.Scale, 6);
    }

    [Fact]
    public void S14319_a_held_template_sizes_the_block_whatever_it_shows()
    {
        // A running time measured by its widest form: the box is the template's, so the layout never moves as it runs.
        var held = new PartBox(150, 36, new LineInk(9, 7), Left: 1, Right: 1, Text: 28);
        var a = BlockLayout.Solve(400, 120, [held], null, ContentAlign.Center);
        var b = BlockLayout.Solve(400, 120, [held], null, ContentAlign.Center);
        Assert.Equal(a.Fill, b.Fill);
        Assert.Equal(a.Places[0], b.Places[0]);
        Assert.Equal((400 - (148 * a.Fill.Scale)) / 2, (a.Places[0]!.Value.X + 1) * a.Fill.Scale, 6);
    }

    [Fact]
    public void S12814_a_room_shorter_than_the_parts_keeps_them_packed_from_the_top()
    {
        var solved = BlockLayout.Solve(300, 30, [new PartBox(100, 80, new LineInk(6, 4))], null, ContentAlign.Center);
        Assert.Equal(1, solved.Fill.Scale);
        Assert.Equal(-6, solved.Places[0]!.Value.Y, 6);
    }

    [Fact]
    public void S12808_spacing_stands_between_the_parts_and_scales_with_them()
    {
        var solved = BlockLayout.Solve(600, 2 * (36 + 4 + 16 - 9.2 - 4), [Reading, Caption with { GivesWayAt = 0 }], null, ContentAlign.Center, spacing: 4);
        Assert.Equal(2, solved.Fill.Scale, 6);
        Assert.Equal(solved.Places[0]!.Value.Y + 40, solved.Places[1]!.Value.Y, 6);
    }

    [Fact]
    public void S12808_an_unmeasured_room_draws_the_parts_at_their_own_size()
    {
        var solved = BlockLayout.Solve(double.PositiveInfinity, double.PositiveInfinity, [Reading, Caption], Face, ContentAlign.Center);
        Assert.Equal(0, solved.Floor);
        Assert.Equal(1, solved.Fill.Scale);
        Assert.True(solved.Shown(0) && solved.Shown(1));
    }

    [Fact]
    public void S12808_a_part_with_no_box_takes_no_slot()
    {
        var solved = BlockLayout.Solve(600, 104, [Reading, new PartBox(0, 0), Caption with { GivesWayAt = 0 }], null, ContentAlign.Center, spacing: 4);
        Assert.False(solved.Shown(1));
        Assert.Equal(solved.Places[0]!.Value.Y + 40, solved.Places[2]!.Value.Y, 6);
    }
}
