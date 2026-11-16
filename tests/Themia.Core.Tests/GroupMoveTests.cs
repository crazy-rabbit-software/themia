using Themia.Core.Geometry;
using Themia.Core.Model;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-11873] A block moved freely moves by whole device pixels, so its cards keep their spacing to the
/// pixel; [S-2459] a group's members show their corners in edit mode.
/// </summary>
public sealed class GroupMoveTests
{
    private static readonly ScreenMetrics Screen = new(2560 / 1.5, 1440 / 1.5);

    /// <summary>A column of a group and one card beside it, at fractions that are no whole DIPs.</summary>
    private static readonly FracRect[] Group =
    [
        new(0.1034, 0.1210, 0.1301, 0.0911),
        new(0.1034, 0.2121, 0.1301, 0.0733),
        new(0.2335, 0.1210, 0.0977, 0.1644),
    ];

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    [InlineData(2.0)]
    [InlineData(2.5)]
    public void S11873_a_group_moved_freely_keeps_every_cards_pixels_relative_to_the_others(double scale)
    {
        var start = Group.Select(r => GridGeometry.ToPixels(r, Screen, scale)).ToArray();

        // A drag in small fractional steps across the screen, both ways, as the pointer reports it.
        for (var i = 1; i < 240; i++)
        {
            var dx = (i * 0.00137) - 0.11;
            var dy = (i * 0.00071) - 0.05;
            var delta = BlockGeometry.Move(Group, dx, dy, Screen, rasterScale: scale);
            var moved = Group.Select(r => GridGeometry.ToPixels(BlockGeometry.Apply(r, delta, Screen), Screen, scale)).ToArray();

            var (ox, oy) = (moved[0].X - start[0].X, moved[0].Y - start[0].Y);
            for (var c = 0; c < Group.Length; c++)
            {
                Assert.Equal(start[c].Width, moved[c].Width, 9);
                Assert.Equal(start[c].Height, moved[c].Height, 9);
                Assert.Equal(start[c].X + ox, moved[c].X, 9);
                Assert.Equal(start[c].Y + oy, moved[c].Y, 9);
            }

            // One offset, in whole device pixels.
            Assert.Equal(Math.Round(ox * scale), ox * scale, 6);
            Assert.Equal(Math.Round(oy * scale), oy * scale, 6);
        }
    }

    [Fact]
    public void S11873_the_regression_rounded_each_card_on_its_own_and_drifted()
    {
        // The old move: the drag's fraction itself, each card then rounded on its own at 150 %.
        var start = Group.Select(r => GridGeometry.ToPixels(r, Screen, 1.5)).ToArray();
        var drifted = false;
        for (var i = 1; i < 120 && !drifted; i++)
        {
            var delta = BlockGeometry.Move(Group, i * 0.00113, 0, Screen);
            var moved = Group.Select(r => GridGeometry.ToPixels(BlockGeometry.Apply(r, delta, Screen), Screen, 1.5)).ToArray();
            drifted = Enumerable.Range(1, Group.Length - 1).Any(c => Math.Abs((moved[c].X - moved[0].X) - (start[c].X - start[0].X)) > 1e-9);
        }

        Assert.True(drifted, "without the display's scale the move is the drag's own fraction, which drifts");
    }

    [Theory]
    [InlineData(1.0, 1)]
    [InlineData(1.25, 4)]
    [InlineData(1.5, 2)]
    [InlineData(1.75, 4)]
    [InlineData(2.0, 1)]
    [InlineData(2.25, 4)]
    public void S11873_a_pixel_step_is_the_fewest_whole_dips_that_are_whole_pixels(double scale, int step)
        => Assert.Equal(step, GridGeometry.PixelStep(scale));


    [Fact]
    public void S11873_a_block_at_its_limit_stops_there_whole()
    {
        // Far past the right edge: the block stops where its last card keeps the visible minimum.
        var free = BlockGeometry.Move(Group, 5, 0, Screen);
        var whole = BlockGeometry.Move(Group, 5, 0, Screen, rasterScale: 1.5);
        Assert.True(whole.Dx <= free.Dx + 1e-12);
        Assert.True(free.Dx - whole.Dx < 2.0 / Screen.Width);
    }

    [Fact]
    public void S11873_a_whole_dip_and_its_pixel_round_half_up()
    {
        Assert.Equal(3, GridGeometry.WholeDip(2.5));
        Assert.Equal(-2, GridGeometry.WholeDip(-2.5));
        Assert.Equal(5 / 1.5, GridGeometry.DevicePixel(3, 1.5), 9);
        Assert.Equal(8 / 1.5, GridGeometry.DevicePixel(5, 1.5), 9);
    }

    // ---- [S-2459] a member's corners ----

    [Fact]
    public void S2459_a_lone_widget_and_the_entered_selected_member_draw_their_handles()
    {
        Assert.Equal(EditMarks.Handles, SelectionRules.MarksOf(group: null, entered: null, selected: false));
        Assert.Equal(EditMarks.Handles, SelectionRules.MarksOf(group: null, entered: "g", selected: true));
        Assert.Equal(EditMarks.Handles, SelectionRules.MarksOf(group: "g", entered: "g", selected: true));
    }

    [Theory]
    [InlineData(null, false)] // the group neither selected nor entered
    [InlineData(null, true)] // the group selected whole: every member is in the selection
    [InlineData("g", false)] // another member of the group entered
    [InlineData("h", false)] // another group entered
    [InlineData("h", true)]
    public void S2459_every_other_member_shows_its_corners(string? entered, bool selected)
        => Assert.Equal(EditMarks.Corners, SelectionRules.MarksOf("g", entered, selected));
}
