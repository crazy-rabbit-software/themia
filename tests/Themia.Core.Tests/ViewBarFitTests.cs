using Themia.Core.Geometry;
using Themia.Core.Model;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-917]..[S-918] The view bar never leaves the work area: its room, which chips fit, and where it lands.</summary>
public sealed class ViewBarFitTests
{
    private static readonly PixelRect Area = new(0, 0, 960, 492);

    private static double[] Chips(int count, double length = 40) => [.. Enumerable.Repeat(length, count)];

    [Fact]
    public void S917_a_row_has_the_area_width_less_both_margins()
    {
        Assert.Equal(960 - (2 * ViewBarRules.EdgeMargin), ViewBarRules.Room(Area, ViewBarAnchor.BottomCenter));
        Assert.Equal(960 - (2 * ViewBarRules.EdgeMargin), ViewBarRules.Room(Area, ViewBarAnchor.TopLeft, new PixelRect(250, 8, 460, 44)));
    }

    [Fact]
    public void S917_a_column_has_the_area_height_less_the_margins_and_the_toolbar_strip()
    {
        var m = ViewBarRules.EdgeMargin;
        Assert.Equal(492 - (2 * m), ViewBarRules.Room(Area, ViewBarAnchor.LeftCenter));

        var toolbar = new PixelRect(250, 8, 460, 44);
        var room = ViewBarRules.Room(Area, ViewBarAnchor.RightCenter, toolbar);
        Assert.Equal(492 - (2 * m) - (8 + 44 + (m / 2) - m), room);
    }

    [Fact]
    public void S917_a_tiny_area_has_no_negative_room()
        => Assert.Equal(0, ViewBarRules.Room(new PixelRect(0, 0, 10, 10), ViewBarAnchor.LeftCenter, new PixelRect(0, 0, 10, 40)));

    [Fact]
    public void S917_chips_that_fit_are_all_drawn_without_an_overflow_chip()
    {
        var fit = ViewBarRules.Fit(Chips(5), shown: 2, room: 1000, fixedLength: 8, spacing: 4, overflowLength: 28);

        Assert.False(fit.Overflow);
        Assert.Equal([0, 1, 2, 3, 4], fit.Shown);
    }

    [Fact]
    public void S917_exactly_fitting_chips_do_not_overflow()
    {
        // 3 x 40 + 2 x 4 + 8 = 136.
        var fit = ViewBarRules.Fit(Chips(3), shown: 0, room: 136, fixedLength: 8, spacing: 4, overflowLength: 28);
        Assert.False(fit.Overflow);
        Assert.Equal(3, fit.Shown.Count);
    }

    [Fact]
    public void S917_too_many_chips_keep_the_first_that_fit_beside_the_overflow_chip()
    {
        // Budget 300 - 8 - 28 - 4 = 260: six chips of 40 with five gaps of 4 = 260.
        var fit = ViewBarRules.Fit(Chips(60), shown: 0, room: 300, fixedLength: 8, spacing: 4, overflowLength: 28);

        Assert.True(fit.Overflow);
        Assert.Equal([0, 1, 2, 3, 4, 5], fit.Shown);
    }

    [Fact]
    public void S917_the_shown_view_is_always_drawn_and_order_is_kept()
    {
        var fit = ViewBarRules.Fit(Chips(60), shown: 41, room: 300, fixedLength: 8, spacing: 4, overflowLength: 28);

        Assert.True(fit.Overflow);
        Assert.Contains(41, fit.Shown);
        Assert.Equal([0, 1, 2, 3, 4, 41], fit.Shown);
    }

    [Fact]
    public void S917_the_drawn_chips_and_the_overflow_chip_never_overrun_the_room()
    {
        var random = new Random(917);
        for (var run = 0; run < 500; run++)
        {
            var lengths = Enumerable.Range(0, random.Next(1, 70)).Select(_ => 28 + (random.NextDouble() * 220)).ToArray();
            var shown = random.Next(-1, lengths.Length);
            var room = 300 + (random.NextDouble() * 2000);

            var fit = ViewBarRules.Fit(lengths, shown, room, fixedLength: 10, spacing: 4, overflowLength: 28);
            var drawn = 10 + fit.Shown.Sum(i => lengths[i]) + (4 * Math.Max(0, fit.Shown.Count - 1)) + (fit.Overflow ? 4 + 28 : 0);

            Assert.True(drawn <= room + 1e-9, $"run {run}: {drawn} > {room}");
            Assert.Equal(fit.Shown.Order(), fit.Shown);
            Assert.Equal(fit.Shown.Count < lengths.Length, fit.Overflow);
            if (shown >= 0)
            {
                Assert.Contains(shown, fit.Shown);
            }
        }
    }

    [Fact]
    public void S917_a_shown_chip_longer_than_the_room_is_still_drawn()
    {
        var fit = ViewBarRules.Fit([500, 40, 40], shown: 0, room: 100, fixedLength: 8, spacing: 4, overflowLength: 28);

        Assert.True(fit.Overflow);
        Assert.Equal([0], fit.Shown);
    }

    [Fact]
    public void S917_no_chips_is_no_overflow()
    {
        var fit = ViewBarRules.Fit([], shown: -1, room: 100, fixedLength: 8, spacing: 4, overflowLength: 28);
        Assert.False(fit.Overflow);
        Assert.Empty(fit.Shown);
    }

    [Theory]
    [InlineData(ViewBarAnchor.TopLeft, ViewBarMenuSide.Below)]
    [InlineData(ViewBarAnchor.TopCenter, ViewBarMenuSide.Below)]
    [InlineData(ViewBarAnchor.TopRight, ViewBarMenuSide.Below)]
    [InlineData(ViewBarAnchor.BottomLeft, ViewBarMenuSide.Above)]
    [InlineData(ViewBarAnchor.BottomCenter, ViewBarMenuSide.Above)]
    [InlineData(ViewBarAnchor.BottomRight, ViewBarMenuSide.Above)]
    [InlineData(ViewBarAnchor.LeftCenter, ViewBarMenuSide.Right)]
    [InlineData(ViewBarAnchor.RightCenter, ViewBarMenuSide.Left)]
    public void S918_the_overflow_menu_opens_into_the_display(ViewBarAnchor anchor, ViewBarMenuSide side)
        => Assert.Equal(side, ViewBarRules.MenuSide(anchor));

    [Fact]
    public void S918_a_menu_is_capped_to_the_work_area_on_its_side_of_the_bar()
    {
        var m = ViewBarRules.EdgeMargin;
        var bottomBar = new PixelRect(16, 444, 600, 32);
        Assert.Equal(444 - m, ViewBarRules.MenuHeight(Area, bottomBar, ViewBarMenuSide.Above));

        var toolbar = new PixelRect(250, 8, 460, 44);
        Assert.Equal(492 - 52 - m, ViewBarRules.MenuHeight(Area, toolbar, ViewBarMenuSide.Below));

        Assert.Equal(492 - (2 * m), ViewBarRules.MenuHeight(Area, new PixelRect(16, 30, 40, 400), ViewBarMenuSide.Right));
        Assert.Equal(0, ViewBarRules.MenuHeight(Area, new PixelRect(16, 0, 40, 10), ViewBarMenuSide.Above));
    }

    [Theory]
    [InlineData(ViewBarAnchor.TopLeft)]
    [InlineData(ViewBarAnchor.TopCenter)]
    [InlineData(ViewBarAnchor.TopRight)]
    [InlineData(ViewBarAnchor.LeftCenter)]
    [InlineData(ViewBarAnchor.RightCenter)]
    [InlineData(ViewBarAnchor.BottomLeft)]
    [InlineData(ViewBarAnchor.BottomCenter)]
    [InlineData(ViewBarAnchor.BottomRight)]
    public void S917_a_bar_as_long_as_its_room_lands_wholly_inside_the_work_area_at_every_anchor(ViewBarAnchor anchor)
    {
        // A display with the taskbar on the left and an offset origin (a second monitor), edit mode on.
        var area = new PixelRect(1968, 40, 1872, 1040);
        var toolbar = new PixelRect(area.X + ((area.Width - 460) / 2), area.Y + 8, 460, 44);
        var vertical = ViewBarRules.IsVertical(anchor);

        foreach (var avoid in new PixelRect?[] { null, toolbar })
        {
            var room = ViewBarRules.Room(area, anchor, avoid);
            var (width, height) = vertical ? (48.0, room) : (room, 48.0);
            var (x, y) = ViewBarRules.Place(area, width, height, anchor, avoid);

            Assert.InRange(x, area.X, area.X + area.Width - width);
            Assert.InRange(y, area.Y, area.Y + area.Height - height);
            if (avoid is { } chrome)
            {
                Assert.Null(GridGeometry.Intersect(new PixelRect(x, y, width, height), chrome));
            }
        }
    }
}
