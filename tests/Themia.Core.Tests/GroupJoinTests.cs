using Themia.Core.Geometry;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-2248] When two cards are neighbours (their cells meet), [S-2249] the members of one group whose
/// cells meet paint as one connected panel (there is no gap between widgets, [S-11884]: every card paints
/// its whole cell), [S-2240] cards of no group that touch stay separate, and [S-11810] a group's frame is
/// its members' own card edges on whole device pixels.
/// </summary>
public sealed class GroupJoinTests
{
    private static CardOutline[] Paint(PixelRect[] cells, string?[] groups, double scale = 1, int?[]? sets = null)
        => CardJoins.Paint(cells, sets ?? [.. cells.Select(_ => (int?)0)], groups, scale);

    private static bool OnDevicePixel(double dip, double scale) => Math.Abs((dip * scale) - Math.Round(dip * scale)) < 1e-6;

    // ---- [S-2248] neighbours ----

    [Fact]
    public void S2248_cells_that_share_a_side_are_neighbours_and_say_which_is_first()
    {
        Assert.Equal((false, true), CardJoins.Meeting(new PixelRect(0, 0, 100, 50), new PixelRect(100, 0, 80, 50), 0.5));
        Assert.Equal((false, false), CardJoins.Meeting(new PixelRect(100, 0, 80, 50), new PixelRect(0, 0, 100, 50), 0.5));
        Assert.Equal((true, true), CardJoins.Meeting(new PixelRect(0, 0, 100, 50), new PixelRect(20, 50, 40, 30), 0.5));
    }

    [Fact]
    public void S2248_a_corner_alone_or_a_sliver_along_the_side_is_no_meeting()
    {
        Assert.Null(CardJoins.Meeting(new PixelRect(0, 0, 100, 50), new PixelRect(100, 50, 80, 50), 0.5));
        Assert.Null(CardJoins.Meeting(new PixelRect(0, 0, 100, 50), new PixelRect(100, 49.6, 80, 50), 0.5));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2)]
    public void S2248_one_device_pixel_apart_or_over_is_no_meeting_at_any_scale(double scale)
    {
        var tol = CardJoins.Tolerance(scale);
        var px = 1 / scale;
        Assert.NotNull(CardJoins.Meeting(new PixelRect(0, 0, 100, 50), new PixelRect(100, 0, 80, 50), tol));
        Assert.Null(CardJoins.Meeting(new PixelRect(0, 0, 100, 50), new PixelRect(100 + px, 0, 80, 50), tol));
        Assert.Null(CardJoins.Meeting(new PixelRect(0, 0, 100, 50), new PixelRect(100 - px, 0, 80, 50), tol));
    }

    [Theory]
    [InlineData(1920, 1080, 1)]
    [InlineData(2560 / 1.5, 1440 / 1.5, 1.5)]
    [InlineData(1920 / 1.25, 1080 / 1.25, 1.25)]
    [InlineData(3840 / 1.75, 2160 / 1.75, 1.75)]
    public void S2248_cells_on_the_grid_that_share_a_line_meet_on_one_device_pixel_at_fractional_dips(double width, double height, double scale)
    {
        // Every pair of neighbouring cells of the snap grid, as fractions of a screen whose DIPs are no whole multiple of the cell.
        var screen = new ScreenMetrics(width, height);
        var (cols, rows) = GridGeometry.GridDims(screen);
        for (var c = 1; c + 3 < cols; c += 7)
        {
            var left = GridGeometry.ToPixels(new FracRect((c - 1) / (double)cols, 0.1, 1.0 / cols, 3.0 / rows), screen, scale);
            var right = GridGeometry.ToPixels(new FracRect(c / (double)cols, 0.1, 3.0 / cols, 3.0 / rows), screen, scale);
            Assert.NotNull(CardJoins.Meeting(left, right, CardJoins.Tolerance(scale)));
            Assert.True(OnDevicePixel(right.X, scale));
            Assert.Equal(left.X + left.Width, right.X, 9);
        }
    }

    // ---- [S-2249] a group is one panel, lone cards stay cards ----

    [Fact]
    public void S2249_regression_grouping_cards_placed_border_to_border_joins_them_into_one_panel()
    {
        // Owner, 2026-11-15: two widgets put border to border, then grouped, did not connect.
        PixelRect[] cells = [new(0, 0, 200, 120), new(200, 0, 160, 120)];

        var grouped = Paint(cells, ["g", "g"]);
        Assert.False(grouped[0].DrawsRight);
        Assert.False(grouped[1].DrawsLeft);
        Assert.False(grouped[0].RoundTopRight || grouped[0].RoundBottomRight);
        Assert.False(grouped[1].RoundTopLeft || grouped[1].RoundBottomLeft);
        Assert.True(grouped[0].RoundTopLeft && grouped[1].RoundBottomRight);
    }

    [Fact]
    public void S2240_S11884_lone_cards_that_touch_stay_two_cards_with_all_their_corners()
    {
        // Owner, 2026-11-15: "the space between widgets will only come from how you place them now": two
        // ungrouped cards placed edge to edge are two cards, never one surface with a shared line.
        PixelRect[] cells = [new(0, 0, 100, 50), new(100, 0, 100, 50), new(0, 50, 200, 40)];
        Assert.All(Paint(cells, [null, null, null]), o => Assert.Equal(CardOutline.Free, o));

        // Members of two different groups are not mates either.
        Assert.All(Paint(cells, ["a", "b", "c"]), o => Assert.Equal(CardOutline.Free, o));
    }

    [Fact]
    public void S2249_a_three_member_panel_squares_its_inner_corners_and_keeps_its_outer_ones()
    {
        PixelRect[] cells = [new(0, 0, 200, 120), new(200, 0, 160, 120), new(0, 120, 360, 60)];
        var p = Paint(cells, ["g", "g", "g"]);
        Assert.False(p[0].DrawsRight || p[1].DrawsLeft || p[0].DrawsBottom || p[2].DrawsTop);
        Assert.True(p[0].RoundTopLeft && p[1].RoundTopRight && p[2].RoundBottomLeft && p[2].RoundBottomRight);
        Assert.False(p[0].RoundBottomLeft || p[1].RoundBottomRight || p[2].RoundTopLeft || p[2].RoundTopRight);
    }

    [Fact]
    public void S2249_members_that_are_not_neighbours_stay_separate_cards()
    {
        PixelRect[] cells = [new(0, 0, 100, 50), new(300, 200, 100, 50)];
        var p = Paint(cells, ["g", "g"]);
        Assert.Equal(CardOutline.Free, p[0]);
        Assert.Equal(CardOutline.Free, p[1]);
    }

    [Fact]
    public void S2249_a_dragged_member_or_one_being_resized_is_free()
    {
        PixelRect[] cells = [new(0, 0, 100, 50), new(100, 0, 100, 50)];
        Assert.Equal(CardOutline.Free, Paint(cells, ["g", "g"], sets: [0, 1])[0]);
        Assert.Equal(CardOutline.Free, Paint(cells, ["g", "g"], sets: [0, null])[0]);

        // A group dragged whole is one set: still one panel.
        Assert.False(Paint(cells, ["g", "g"], sets: [1, 1])[0].DrawsRight);
    }

    [Fact]
    public void S2249_a_lone_card_beside_a_group_keeps_its_own_corners()
    {
        // A wide member over a narrow mate and a lone card: only the corner over the mate is square.
        PixelRect[] cells = [new(0, 0, 200, 100), new(0, 100, 100, 50), new(100, 100, 100, 50)];
        var p = Paint(cells, ["g", "g", null]);
        Assert.False(p[0].RoundBottomLeft);
        Assert.True(p[0].RoundBottomRight);
        Assert.Equal(CardOutline.Free, p[2]);
    }

    [Fact]
    public void S11880_a_card_that_never_joins_is_free_beside_its_group_mates()
    {
        // A desktop icon (WidgetTraits.NeverJoins) takes no part in joins whatever its fill, even grouped.
        Assert.False(CardJoins.Joins(Themia.Sdk.WidgetTraits.NeverJoins, drawsSurface: true));
        Assert.False(CardJoins.Joins(Themia.Sdk.WidgetTraits.StartsTransparent | Themia.Sdk.WidgetTraits.NeverJoins, drawsSurface: true));
        Assert.True(CardJoins.Joins(Themia.Sdk.WidgetTraits.None, drawsSurface: true));
        Assert.False(CardJoins.Joins(Themia.Sdk.WidgetTraits.None, drawsSurface: false));

        PixelRect[] cells = [new(0, 0, 100, 100), new(100, 0, 100, 100)];
        Assert.All(Paint(cells, ["g", "g"], sets: [null, null]), o => Assert.Equal(CardOutline.Free, o));
    }

    [Fact]
    public void S11880_regression_an_icon_on_no_card_is_not_clipped_to_a_cards_corners()
    {
        // Owner, 2026-11-15: "the desktop icons have two rounded and two normal corners": the highlight's top
        // two were clipped to the card's 8, its bottom two its own 2. An icon on no card clips square.
        Assert.Equal(0, CardJoins.ClipRadius(Themia.Sdk.WidgetTraits.StartsTransparent | Themia.Sdk.WidgetTraits.NeverJoins, drawsSurface: false, 8));
        Assert.Equal(8, CardJoins.ClipRadius(Themia.Sdk.WidgetTraits.NeverJoins, drawsSurface: true, 8));
        Assert.Equal(8, CardJoins.ClipRadius(Themia.Sdk.WidgetTraits.None, drawsSurface: false, 8));
    }

    // ---- [S-11810] the group's frame ----

    [Theory]
    [InlineData(1)]
    [InlineData(1.5)]
    [InlineData(1.25)]
    [InlineData(2)]
    public void S11810_S2453_a_groups_frame_is_round_its_members_cells_on_the_grids_lines(double scale)
    {
        var screen = new ScreenMetrics(2560 / 1.5, 1440 / 1.5);
        var (cols, rows) = GridGeometry.GridDims(screen);
        FracRect Cells(int c, int r, int w, int h) => new(c / (double)cols, r / (double)rows, w / (double)cols, h / (double)rows);
        PixelRect[] cells = [.. new[] { Cells(11, 7, 9, 5), Cells(20, 7, 6, 5), Cells(11, 12, 15, 3) }.Select(f => GridGeometry.ToPixels(f, screen, scale))];

        var frame = CardJoins.Frame(cells)!.Value;

        // The members' cells, the edges each one's own markers are centred on and the cards are painted to.
        Assert.Equal(cells.Min(c => c.X), frame.X, 9);
        Assert.Equal(cells.Max(c => c.X + c.Width), frame.X + frame.Width, 9);
        Assert.Equal(cells.Min(c => c.Y), frame.Y, 9);
        Assert.Equal(cells.Max(c => c.Y + c.Height), frame.Y + frame.Height, 9);
        Assert.True(OnDevicePixel(frame.X, scale) && OnDevicePixel(frame.Y, scale) && OnDevicePixel(frame.X + frame.Width, scale) && OnDevicePixel(frame.Y + frame.Height, scale));

        // The grid's lines: the whole DIP the overlay puts each on, on its device pixel.
        double Line(int i, int count, double length) => GridGeometry.DevicePixel(GridFit.LineDip(i, count, length), scale);
        Assert.Equal(Line(11, cols, screen.Width), frame.X, 9);
        Assert.Equal(Line(26, cols, screen.Width), frame.X + frame.Width, 9);
        Assert.Equal(Line(7, rows, screen.Height), frame.Y, 9);
        Assert.Equal(Line(15, rows, screen.Height), frame.Y + frame.Height, 9);
    }

    [Fact]
    public void S11810_no_member_no_frame()
        => Assert.Null(CardJoins.Frame([]));
}
