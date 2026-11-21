using Themia.Core.Geometry;
using Themia.Core.Model;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-11890] A move touches only what changed: pointer moves coalesced to one step a frame, the landing reported
/// changed only on another cell, the local grid as clipped layers. [S-11891] The cards are drawn on the grid while dragged, where they land.
/// </summary>
public sealed class DragPipelineTests
{
    /// <summary>1000 DIPs square: 100 grid cells of 10 DIPs a side, one cell 0.01, one DIP 0.001.</summary>
    private static readonly ScreenMetrics Screen = new(1000, 1000);

    private static FracRect R(double x, double y, double w, double h) => new(x, y, w, h);

    [Fact]
    public void S11890_pointer_moves_are_coalesced_to_the_latest_one_a_frame()
    {
        var frames = new PointerFrames();
        Assert.Null(frames.Take());

        // The first move asks for a frame; the ones before it is drawn only update where the pointer is.
        Assert.True(frames.Offer(0.01, 0.02));
        Assert.False(frames.Offer(0.03, 0.04));
        Assert.False(frames.Offer(0.05, 0.06));
        Assert.True(frames.Pending);

        Assert.Equal((0.05, 0.06), frames.Take());
        Assert.False(frames.Pending);
        Assert.Null(frames.Take());

        // The next move after the frame asks again.
        Assert.True(frames.Offer(0.07, 0.08));
    }

    [Fact]
    public void S11890_a_step_says_the_block_moved_only_when_it_reaches_another_cell()
    {
        var move = new BlockMove([R(0.4, 0.4, 0.1, 0.1)], Screen);
        var moves = 0;

        // 30 DIPs to the right, one DIP at a time: it moves on to the 10, 20 and 30 DIP cells (the snap's half cell).
        for (var dip = 1; dip <= 30; dip++)
        {
            moves += move.Step(dip * 0.001, 0) ? 1 : 0;
        }

        Assert.Equal(3, moves);
        Assert.Equal(0.03, move.Landing.Dx, 9);

        // The same place again changes nothing.
        Assert.False(move.Step(0.03, 0));
    }

    [Theory]
    [InlineData(1.0, 13.3)]
    [InlineData(1.0, 4.9)]
    [InlineData(1.5, 13.3)]
    [InlineData(1.5, 27.6)]
    [InlineData(1.25, 13.3)]
    public void S11891_the_dragged_block_is_drawn_on_the_grid_never_between_its_lines(double scale, double pointerDip)
    {
        // Regression (owner, 2026-11-19: "the items dont stick to the grid anymore when moving in the preview"): the
        // one translation the cards are drawn at is the snapped move, so mid-drag they stand where they will land.
        var start = new[] { R(0.4, 0.4, 0.1, 0.1), R(0.5, 0.4, 0.1, 0.1) };
        var move = new BlockMove(start, Screen);
        move.Step(pointerDip / 1000, pointerDip / 2000);

        Assert.Equal(BlockGeometry.Move(start, pointerDip / 1000, pointerDip / 2000, Screen), move.Landing);

        // [S-11892] On the 10 DIP lines, at every scale.
        var x = move.LandingBounds().X * 1000;
        Assert.Equal(Math.Round(x / 10) * 10, x, 9);

        // [S-11873] The two cards share a line, so its pixel: they still touch.
        var left = GridGeometry.ToPixels(move.At(start[0], move.Landing), Screen, scale);
        var right = GridGeometry.ToPixels(move.At(start[1], move.Landing), Screen, scale);
        Assert.Equal(left.X + left.Width, right.X, 9);
    }

    [Fact]
    public void S2008_a_move_goes_where_the_pointer_takes_it_over_any_widget()
    {
        // Owner, 2026-11-21: "keep widgets from overlapping should not be a selectable option anymore". A move never stops
        // flush by a neighbour nor slides along it: the block lands where the pointer takes it, on the grid, over whatever
        // stands there (a neighbour at 0.3..0.4 here, which kept apart once held it at Dx 0.1).
        var start = new[] { R(0.1, 0.1, 0.1, 0.1) };
        var move = new BlockMove(start, Screen);

        move.Step(0.25, 0.016);

        Assert.Equal(BlockGeometry.Move(start, 0.25, 0.016, Screen), move.Landing);
        Assert.Equal(0.25, move.Landing.Dx, 9);
        Assert.Equal(0.02, move.Landing.Dy, 9);

        // The arrow keys alike: one cell a press, whatever is there.
        var nudged = KeyboardNudge.Move([R(0.29, 0.1, 0.1, 0.1)], Shell.NudgeDirection.Right, Shell.NudgeStep.Cell, Screen);
        Assert.Equal(0.3, nudged[0].X, 9);
    }

    [Theory]
    [InlineData(0.42, 0.33, 0.3, 0.24)]
    [InlineData(0.0, 0.0, 0.05, 0.05)]
    [InlineData(0.7, 0.81, 0.2, 0.12)]
    public void S11886_the_local_grids_layers_draw_every_point_at_its_strength_rounded_up_to_a_step(double x, double y, double w, double h)
    {
        var screen = new ScreenMetrics(1280, 720);
        var target = R(x, y, w, h);
        var levels = LocalGrid.Levels(target, screen);
        var box = LocalGrid.Lit(target, screen);
        var radius = LocalGrid.RadiusDip(screen);
        Assert.Equal(LocalGrid.Steps, levels.Count);

        for (var px = box.X - radius - 20; px <= box.X + box.Width + radius + 20; px += 3.7)
        {
            for (var py = box.Y - radius - 20; py <= box.Y + box.Height + radius + 20; py += 3.1)
            {
                var strength = LocalGrid.Strength(box, px, py, radius) * LocalGrid.Steps;
                if (Math.Abs(strength - Math.Round(strength)) < 1e-6 && strength > 0.5)
                {
                    continue; // on a step's edge: either side is right
                }

                var clear = 1.0;
                foreach (var level in levels)
                {
                    var r = level.Region;
                    if (px > r.X && px < r.X + r.Width && py > r.Y && py < r.Y + r.Height)
                    {
                        clear *= 1 - level.Opacity;
                    }
                }

                Assert.Equal(LocalGrid.PeakOpacity * Math.Ceiling(strength - 1e-9) / LocalGrid.Steps, 1 - clear, 9);
            }
        }
    }

    [Fact]
    public void S11874_the_local_grids_layers_nest_from_the_fade_radius_in_to_the_box()
    {
        var screen = new ScreenMetrics(1280, 720);
        var target = R(0.4, 0.4, 0.2, 0.2);
        var levels = LocalGrid.Levels(target, screen);
        var box = LocalGrid.Lit(target, screen);
        var radius = LocalGrid.RadiusDip(screen);

        Assert.Equal(box.X - radius, levels[0].Region.X, 9);
        Assert.Equal(box.Width + (2 * radius), levels[0].Region.Width, 9);
        for (var k = 1; k < levels.Count; k++)
        {
            Assert.True(levels[k].Region.X > levels[k - 1].Region.X);
            Assert.True(levels[k].Region.X < box.X);
        }

        Assert.Empty(LocalGrid.Levels(target, new ScreenMetrics(0, 0)));
    }
}
