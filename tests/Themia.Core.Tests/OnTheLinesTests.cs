using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-11892] Every card stands on its display's grid lines (owner, 2026-11-20: "there is a problem with the layouting in
/// this configuraiton. the borders dont nicely connect."): the display's own DIPs, never its window a pixel short; a
/// block's move lands on the lines at any scale; touching cards laid apart meet on one line, so they paint one body.
/// </summary>
public sealed class OnTheLinesTests
{
    /// <summary>Whether <paramref name="frac"/> is on one of <paramref name="lines"/> grid lines.</summary>
    private static bool OnLine(double frac, int lines) => Math.Abs((frac * lines) - Math.Round(frac * lines)) < 1e-9;

    private static void OnLines(FracRect r, ScreenMetrics screen)
    {
        var (cols, rows) = GridGeometry.GridDims(screen);
        Assert.True(OnLine(r.X, cols) && OnLine(r.Right, cols), $"x {r.X * cols:0.######} .. {r.Right * cols:0.######} of {cols} columns");
        Assert.True(OnLine(r.Y, rows) && OnLine(r.Bottom, rows), $"y {r.Y * rows:0.######} .. {r.Bottom * rows:0.######} of {rows} rows");
    }

    [Fact]
    public void S11892_a_display_resolves_against_its_bounds_never_its_window_a_pixel_short()
    {
        // The owner's display: 2560 x 1440 at 100 %. Its surface's window is a pixel short at the foot ([S-852]).
        var display = new PixelRect(0, 0, 2560, 1440);
        var screen = GridGeometry.DisplayScreen(display, 1);
        Assert.Equal(new ScreenMetrics(2560, 1440), screen);
        Assert.Equal(10, GridGeometry.CellPitch(screen), 9);
        Assert.Equal(10, screen.Height / GridGeometry.GridDims(screen).Rows, 9);

        // Measured on the window the rows' pitch was no whole DIP: 1439 / 144.
        var window = GridGeometry.SurfaceWindow(display);
        Assert.NotEqual(10, window.Height / GridGeometry.GridDims(new ScreenMetrics(window.Width, window.Height)).Rows, 6);

        // At 150 % the DIPs are the bounds over the scale; a scale that is no number counts as 1.
        Assert.Equal(new ScreenMetrics(2560 / 1.5, 1440 / 1.5), GridGeometry.DisplayScreen(display, 1.5));
        Assert.Equal(new ScreenMetrics(2560, 1440), GridGeometry.DisplayScreen(display, double.NaN));
    }

    /// <summary>Screens whose pitch is no whole number of device pixels: the window a pixel short at 100 %, 125 % and 150 % displays.</summary>
    public static TheoryData<double, double, double> FractionalPitches => new()
    {
        { 2560, 1439, 1 },
        { 1920 / 1.25, 1080 / 1.25, 1.25 },
        { 2560 / 1.5, 1440 / 1.5, 1.5 },
        { 2560 / 1.25, 1440 / 1.25, 1.25 },
    };

    [Theory]
    [MemberData(nameof(FractionalPitches))]
    public void S11892_a_block_moved_lands_on_the_grids_lines_on_a_screen_of_any_pitch(double width, double height, double scale)
    {
        var screen = new ScreenMetrics(width, height);
        var (cols, rows) = GridGeometry.GridDims(screen);

        // A 2 x 2 block of three-cell tiles beside a row of six-cell ones, all on the lines.
        FracRect Cells(int x, int y, int w, int h) => new((double)x / cols, (double)y / rows, (double)w / cols, (double)h / rows);
        FracRect[] block = [Cells(40, 1, 3, 3), Cells(43, 1, 3, 3), Cells(40, 4, 3, 3), Cells(43, 4, 3, 3), Cells(46, 1, 6, 6)];

        for (var i = 1; i < 160; i++)
        {
            var dx = (i * 0.00137) - 0.11;
            var dy = (i * 0.00271) - 0.2;
            var delta = BlockGeometry.Move(block, dx, dy, screen);
            var moved = block.Select(r => BlockGeometry.Apply(r, delta, screen)).ToArray();
            foreach (var r in moved)
            {
                OnLines(r, screen);
            }

            // The tiles' tops and the row's on one pixel, the block's side on the row's.
            var cells = moved.Select(r => GridGeometry.ToPixels(r, screen, scale)).ToArray();
            Assert.Equal(cells[0].Y, cells[4].Y, 9);
            Assert.Equal(cells[1].X + cells[1].Width, cells[4].X, 9);
            Assert.Equal(cells[3].Y + cells[3].Height, cells[4].Y + cells[4].Height, 9);
        }
    }

    [Theory]
    [MemberData(nameof(FractionalPitches))]
    public void S11892_regression_a_block_and_a_row_moved_apart_meet_on_one_line_and_paint_one_body(double width, double height, double scale)
    {
        // The owner's report: a 2 x 2 block of small quick toggles dragged beside a row of four large ones, each moved on
        // its own, met a pixel apart: the block's top on 9 px, the row's on 10, one corner round and one square at the seam.
        var screen = new ScreenMetrics(width, height);
        var (cols, rows) = GridGeometry.GridDims(screen);
        FracRect Cells(int x, int y, int w, int h) => new((double)x / cols, (double)y / rows, (double)w / cols, (double)h / rows);
        FracRect[] small = [Cells(20, 30, 3, 3), Cells(23, 30, 3, 3), Cells(20, 33, 3, 3), Cells(23, 33, 3, 3)];
        FracRect[] large = [Cells(90, 70, 6, 6), Cells(96, 70, 6, 6), Cells(102, 70, 6, 6), Cells(108, 70, 6, 6)];

        // The row dragged to the top right, a third of a DIP short of its lines, then the block dragged against its left
        // side, more than half a DIP short: where the pointer lets go, as fractions of the screen.
        double Near(int line, int lines, double dips, double length) => ((double)line / lines) - (dips / length);
        var rowDelta = BlockGeometry.Move(large, Near(cols - 60, cols, 0.33, width) - large[0].X, Near(1, rows, 0.33, height) - large[0].Y, screen);
        var row = large.Select(r => BlockGeometry.Apply(r, rowDelta, screen)).ToArray();
        var blockDelta = BlockGeometry.Move(small, Near(cols - 66, cols, 0.57, width) - small[0].X, Near(1, rows, 0.57, height) - small[0].Y, screen);
        var block = small.Select(r => BlockGeometry.Apply(r, blockDelta, screen)).ToArray();

        Assert.Equal(row[0].Y, block[0].Y, 12);
        Assert.Equal(row[0].X, block[1].Right, 12);

        var cells = block.Concat(row).Select(r => GridGeometry.ToPixels(r, screen, scale)).ToArray();
        var outlines = CardJoins.Paint(cells, [.. cells.Select(_ => (int?)0)], scale);

        // One body: round only at its four outer corners, no seam left between the block and the row.
        Assert.Equal((true, false, false, false), Corners(outlines[0]));
        Assert.Equal((false, false, false, false), Corners(outlines[1]));
        Assert.Equal((false, false, false, true), Corners(outlines[2]));
        Assert.Equal((false, false, false, false), Corners(outlines[3]));
        Assert.Equal((false, false, false, false), Corners(outlines[4]));
        Assert.Equal((false, true, true, false), Corners(outlines[7]));
        Assert.False(outlines[4].DrawsLeft);
        Assert.False(outlines[1].DrawsRight || outlines[3].DrawsRight);
    }

    // ---- the owner's layout, as stored: put on the lines ----------------------------------------------------------

    /// <summary>The owner's display at 100 %, as the surface measures it now.</summary>
    private static readonly ScreenMetrics Owners = new(2560, 1440);

    /// <summary>
    /// The owner's Home view as stored (2026-11-20), fractions of the monitor: a 2 x 2 block of 30-pixel quick toggles
    /// (Do Not Disturb, Night light; Wi-Fi, Bluetooth) and right of it a row of four 60-pixel ones, touching. The block's
    /// top stood at 9.43 pixels, the row's at 9.67, the block's lower row at 39.45: no edge on a line of 10.
    /// </summary>
    private static readonly FracRect[] OwnersToggles =
    [
        new(0.73046875, 0.006552030260923256, 0.01171875, 0.020833333333333332),
        new(0.7421875, 0.006552030260923256, 0.01171875, 0.020833333333333332),
        new(0.73046875, 0.027395013123359555, 0.01171875, 0.020833333333333332),
        new(0.7421875, 0.027395013123359555, 0.01171875, 0.020833333333333332),
        new(0.75390625, 0.006716072255673874, 0.0234375, 0.041666666666666664),
        new(0.77734375, 0.006716072255673874, 0.0234375, 0.041666666666666664),
        new(0.80078125, 0.006716072255673874, 0.0234375, 0.041666666666666664),
        new(0.82421875, 0.006716072255673874, 0.0234375, 0.041666666666666664),
    ];

    private static CardOutline[] Painted(IEnumerable<FracRect> rects, ScreenMetrics screen, double scale = 1)
    {
        var cells = rects.Select(r => GridGeometry.ToPixels(r, screen, scale)).ToArray();
        return CardJoins.Paint(cells, [.. cells.Select(_ => (int?)0)], scale);
    }

    [Fact]
    public void S11892_regression_the_owners_block_and_row_met_a_pixel_apart()
    {
        // As stored and painted: the block's top on pixel 9, the row's on 10; the block's top right corner round against
        // the row's square top left, the row's bottom left round against the block's square bottom right.
        var cells = OwnersToggles.Select(r => GridGeometry.ToPixels(r, Owners, 1)).ToArray();
        Assert.Equal(9, cells[0].Y);
        Assert.Equal(10, cells[4].Y);
        var outlines = Painted(OwnersToggles, Owners);
        Assert.True(outlines[1].RoundTopRight);
        Assert.False(outlines[4].RoundTopLeft);
        Assert.True(outlines[4].RoundBottomLeft);
        Assert.False(outlines[3].RoundBottomRight);
    }

    [Fact]
    public void S11892_regression_the_owners_block_and_row_are_put_on_the_lines_and_paint_one_body()
    {
        var settled = GridSettle.Settle(OwnersToggles, [.. OwnersToggles.Select(_ => false)], Owners);

        foreach (var r in settled)
        {
            OnLines(r, Owners);
        }

        // On the lines of 10: tops on 10, the block's seam on 40, every foot on 70; sides and sizes as they were.
        Assert.All(settled.Take(2).Concat(settled.Skip(4)), r => Assert.Equal(10, r.Y * 1440, 9));
        Assert.All(settled.Skip(2).Take(2), r => Assert.Equal(40, r.Y * 1440, 9));
        Assert.All(settled.Skip(2), r => Assert.Equal(70, r.Bottom * 1440, 9));
        for (var i = 0; i < settled.Count; i++)
        {
            Assert.Equal(OwnersToggles[i].X, settled[i].X);
            Assert.Equal(OwnersToggles[i].W, settled[i].W);
            Assert.Equal(OwnersToggles[i].H, settled[i].H, 12);
        }

        // One body: round only at its outer corners, square at the seam, no line between the block and the row.
        var outlines = Painted(settled, Owners);
        Assert.Equal((true, false, false, false), Corners(outlines[0]));
        Assert.Equal((false, false, false, false), Corners(outlines[1]));
        Assert.Equal((false, false, false, true), Corners(outlines[2]));
        Assert.Equal((false, false, false, false), Corners(outlines[3]));
        Assert.Equal((false, false, false, false), Corners(outlines[4]));
        Assert.Equal((false, false, false, false), Corners(outlines[5]));
        Assert.Equal((false, false, false, false), Corners(outlines[6]));
        Assert.Equal((false, true, true, false), Corners(outlines[7]));
        Assert.False(outlines[4].DrawsLeft);
        Assert.False(outlines[1].DrawsRight || outlines[3].DrawsRight);

        // Settled is settled.
        Assert.Same(settled, GridSettle.Settle(settled, [.. settled.Select(_ => false)], Owners));
    }

    [Fact]
    public void S11892_edges_that_are_one_edge_move_together_or_not_at_all()
    {
        // A column of two cards drifted 0.4 DIP over lines 10, 30 and 40, their seam on 30.4, and a card on its lines
        // beside them: with both free the seam goes on line 30 for both; the card on its lines is not touched.
        static double Y(double dip) => dip / 1440;
        FracRect[] column = [new(0.1, Y(100.4), 0.1, Y(200)), new(0.1, Y(300.4), 0.1, Y(100)), new(0.3, Y(400), 0.1, Y(100))];
        var free = GridSettle.Settle(column, [false, false, false], Owners);
        Assert.Equal(Y(100), free[0].Y, 12);
        Assert.Equal(Y(300), free[0].Bottom, 12);
        Assert.Equal(Y(300), free[1].Y, 12);
        Assert.Equal(Y(400), free[1].Bottom, 12);
        Assert.Equal(column[2], free[2]);

        // The upper one stays (a fence, laid by its own rule): the lower keeps the seam with it; nothing moves.
        Assert.Same(column, GridSettle.Settle(column, [true, false, false], Owners));

        // A lower card whose foot is far off a line (another display's grid) holds the seam, and the upper card with it;
        // a card apart from them is put on its line all the same.
        FracRect[] held = [column[0], new(0.1, Y(300.4), 0.1, Y(104.6)), new(0.3, Y(0.6), 0.1, Y(30))];
        var settled = GridSettle.Settle(held, [false, false, false], Owners);
        Assert.Equal(held[0], settled[0]);
        Assert.Equal(held[1], settled[1]);
        Assert.Equal(0, settled[2].Y, 12);
    }

    [Fact]
    public void S11892_an_edge_flush_on_the_work_area_or_further_than_a_dip_off_stays()
    {
        // A 48-DIP taskbar at the foot: the work area ends at 1392, two DIPs past line 139.
        var screen = Owners with { Allowed = new FracRect(0, 0, 1, 1392.0 / 1440) };
        FracRect[] rects =
        [
            new(0.1, 1092.0 / 1440, 0.1, 300.0 / 1440),     // flush on the work area's foot ([S-6609]): stays
            new(0.3, 501.5 / 1440, 0.1, 100.0 / 1440),      // 1.5 DIPs off: no drift, stays
            new(0.5, 500.6 / 1440, 0.1, 100.0 / 1440),      // 0.6 DIPs off: put on the line
        ];

        var settled = GridSettle.Settle(rects, [false, false, false], screen);
        Assert.Equal(rects[0], settled[0]);
        Assert.Equal(rects[1], settled[1]);
        Assert.Equal(500.0 / 1440, settled[2].Y, 12);
        Assert.Equal(100.0 / 1440, settled[2].H, 12);
    }

    [Fact]
    public void S11892_a_card_whose_whole_cells_would_change_stays()
    {
        // 0.4 DIP over line 1 at the top and 0.4 over line 4 at the foot: 30.0 DIPs, three cells either way: put on the
        // lines. 0.4 under line 11 and 0.4 over line 14: 30.8 DIPs paint four cells, on the lines three: it stays.
        FracRect[] rects = [new(0.1, 10.4 / 1440, 0.1, 30.0 / 1440), new(0.3, 109.6 / 1440, 0.1, 30.8 / 1440)];
        var settled = GridSettle.Settle(rects, [false, false], Owners);
        Assert.Equal(10.0 / 1440, settled[0].Y, 12);
        Assert.Equal(rects[1], settled[1]);
    }

    private sealed class Card : WidgetConfig
    {
        public override WidgetConfig Clone() => new Card();
    }

    [Fact]
    public void S11892_S854_a_shown_view_is_put_on_the_lines_once_its_display_settled_and_icons_are_left_alone()
    {
        var state = new AppState();
        var view = ViewState.Create("Home");
        view.Id = "home";
        view.Widgets.AddRange(OwnersToggles.Select((r, i) => new WidgetInstance { Id = "t" + i, Config = new Card(), Rect = r }));
        var icon = new WidgetInstance { Id = "icon", Config = new DeskIconFake { Item = @"C:\Users\a\Desktop\a.txt" }, Rect = new FracRect(0.1, 0.0066, 0.0234375, 0.0625) };
        view.Widgets.Add(icon);
        state.Views.Add(view);

        // Not settled: nothing written ([S-854]).
        SurfaceMetrics[] unsettled = [new("home", Owners, 1, Settled: false)];
        Assert.False(GridSettle.Waits(state, unsettled));
        Assert.Empty(GridSettle.Apply(state, unsettled));

        SurfaceMetrics[] settled = [new("home", Owners, 1, Settled: true)];
        Assert.True(GridSettle.Waits(state, settled));
        var notes = GridSettle.Apply(state, settled);

        Assert.Equal(8, notes.Count);
        Assert.Contains("'t0'", notes[0], StringComparison.Ordinal);
        Assert.All(view.Widgets.Take(8), w => OnLines(w.Rect, Owners));
        Assert.Equal(new FracRect(0.1, 0.0066, 0.0234375, 0.0625), icon.Rect);
        Assert.False(GridSettle.Waits(state, settled));
        Assert.Empty(GridSettle.Apply(state, settled));
    }

    private static (bool TopLeft, bool TopRight, bool BottomRight, bool BottomLeft) Corners(CardOutline o)
        => (o.RoundTopLeft, o.RoundTopRight, o.RoundBottomRight, o.RoundBottomLeft);
}
