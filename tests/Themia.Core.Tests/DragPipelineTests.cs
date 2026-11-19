using Themia.Core.Geometry;
using Themia.Core.Model;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-11890] A move touches only what changed: pointer moves coalesced to one step a frame, the keep-apart check
/// looking only at the cards near the block, the landing reported changed only on another cell, the local grid as
/// clipped layers. [S-11891] The cards follow the pointer in whole pixel steps and land on the grid.
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
    public void S11890_the_keep_apart_check_looks_only_at_cards_near_the_block_and_decides_as_the_full_scan()
    {
        var random = new Random(11890);
        for (var round = 0; round < 400; round++)
        {
            var block = Enumerable.Range(0, 1 + random.Next(3)).Select(_ => Rect(random)).ToList();
            var others = Enumerable.Range(0, random.Next(25)).Select(_ => Rect(random)).ToList();
            var wanted = new BlockGeometry.Delta(Cells(random, 30), Cells(random, 30));
            var last = new BlockGeometry.Delta(Cells(random, 30), Cells(random, 30));
            var scale = random.Next(2) == 0 ? (double?)null : 1.5;

            Assert.Equal(FullScanApart(block, wanted, last, others, scale), BlockGeometry.Apart(block, wanted, last, others, Screen, scale));

            // Every card the prune leaves out could not have held any of the three tries.
            var slack = BlockGeometry.Slack(Screen, scale);
            BlockGeometry.Delta[] tries = [wanted, wanted with { Dy = last.Dy }, wanted with { Dx = last.Dx }];
            var near = BlockGeometry.Near(block, tries, others, Screen, slack);
            foreach (var other in others.Except(near))
            {
                Assert.All(tries, d => Assert.True(BlockGeometry.Clear(block, d, [other], Screen, slack)));
            }
        }

        static FracRect Rect(Random random) => R(Cells(random, 90) + 0.05, Cells(random, 90) + 0.05, 0.01 * (1 + random.Next(15)), 0.01 * (1 + random.Next(15)));
        static double Cells(Random random, int span) => 0.01 * (random.Next((2 * span) + 1) - span);
    }

    [Fact]
    public void S11890_a_step_says_where_the_block_lands_changed_only_when_it_reaches_another_cell()
    {
        var move = new BlockMove([R(0.4, 0.4, 0.1, 0.1)], apart: null, Screen, rasterScale: 1);
        var landings = 0;
        var shown = 0;

        // 30 DIPs to the right, one DIP at a time: drawn anew every DIP, landing anew on the 10, 20 and 30 DIP cells.
        for (var dip = 1; dip <= 30; dip++)
        {
            var step = move.Step(dip * 0.001, 0);
            shown += step.ShownChanged ? 1 : 0;
            landings += step.LandingChanged ? 1 : 0;
        }

        Assert.Equal(30, shown);
        Assert.Equal(3, landings);
        Assert.Equal(0.03, move.Landing.Dx, 9);

        // The same place again changes nothing.
        Assert.Equal(new BlockStep(false, false), move.Step(0.03, 0));
    }

    [Theory]
    [InlineData(1.0, 13.3, 13)]
    [InlineData(1.5, 13.3, 14)]
    [InlineData(1.25, 13.3, 12)]
    public void S11891_the_block_is_drawn_at_the_pointer_in_whole_pixel_steps_and_lands_on_the_grid(double scale, double pointerDip, double drawnDip)
    {
        var move = new BlockMove([R(0.4, 0.4, 0.1, 0.1), R(0.5, 0.4, 0.1, 0.1)], apart: null, Screen, scale);
        move.Step(pointerDip / 1000, 0);

        Assert.Equal(drawnDip / 1000, move.Shown.Dx, 9);
        // [S-1584] [S-11873] It lands where a snapped move does: on the 10 DIP line, on whole pixel steps (at 125 %
        // four DIPs, so within one such step of it).
        var landing = BlockGeometry.Move([R(0.4, 0.4, 0.1, 0.1), R(0.5, 0.4, 0.1, 0.1)], pointerDip / 1000, 0, Screen, scale);
        Assert.Equal(landing, move.Landing);
        Assert.InRange(move.Landing.Dx * 1000, 10 - GridGeometry.PixelStep(scale), 10 + GridGeometry.PixelStep(scale));
        var landed = move.LandingBounds();
        Assert.Equal(0.4 + landing.Dx, landed.X, 9);
        Assert.Equal(0.2, landed.W, 9);

        // [S-11873] Drawn there, each card's cell moved by the same whole pixels: the two still touch.
        var left = GridGeometry.ToPixels(move.At(R(0.4, 0.4, 0.1, 0.1), move.Shown), Screen, scale);
        var right = GridGeometry.ToPixels(move.At(R(0.5, 0.4, 0.1, 0.1), move.Shown), Screen, scale);
        Assert.Equal(left.X + left.Width, right.X, 9);
    }

    [Fact]
    public void S11891_kept_apart_the_drawn_block_stops_flush_where_it_lands_and_never_slides_into_a_card()
    {
        var neighbour = R(0.3, 0.1, 0.1, 0.1);
        var move = new BlockMove([R(0.1, 0.1, 0.1, 0.1)], [neighbour], Screen, rasterScale: 1);

        // 4 DIPs past flush: it lands flush (the snap), and is drawn flush too, not 4 DIPs over the neighbour.
        move.Step(0.104, 0);
        Assert.Equal(0.1, move.Landing.Dx, 9);
        Assert.Equal(0.1, move.Shown.Dx, 9);

        // Pushed on along the neighbour's side and down past it, the drawn block slides along the free axis.
        move.Step(0.13, 0.004);
        Assert.Equal(0.1, move.Shown.Dx, 9);
        Assert.Equal(0.004, move.Shown.Dy, 9);
    }

    [Fact]
    public void S11891_far_past_the_screen_the_drawn_block_stops_where_the_landing_does()
    {
        var start = new[] { R(0.4, 0.4, 0.1, 0.1) };
        Assert.Equal(BlockGeometry.Move(start, 5, -5, Screen, 1.5), BlockGeometry.Follow(start, 5, -5, Screen, 1.5));
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

    /// <summary>[S-11959] The keep-apart rule as it was before the prune: every staying card looked at.</summary>
    private static BlockGeometry.Delta FullScanApart(List<FracRect> start, BlockGeometry.Delta wanted, BlockGeometry.Delta last, List<FracRect> others, double? scale)
    {
        var slack = BlockGeometry.Slack(Screen, scale);
        if (others.Count == 0 || BlockGeometry.Clear(start, wanted, others, Screen, slack))
        {
            return wanted;
        }

        var alongX = wanted with { Dy = last.Dy };
        if (BlockGeometry.Clear(start, alongX, others, Screen, slack))
        {
            return alongX;
        }

        var alongY = wanted with { Dx = last.Dx };
        return BlockGeometry.Clear(start, alongY, others, Screen, slack) ? alongY : last;
    }
}
