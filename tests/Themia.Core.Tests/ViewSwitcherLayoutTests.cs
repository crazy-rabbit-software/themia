using Themia.Core.Geometry;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1653]..[S-1656] The view switcher's ladder, overflow, keyboard cursor and sketches.</summary>
public sealed class ViewSwitcherLayoutTests
{
    private static SwitcherLayout Plan(double width, double height, int count = 3, bool iconOnly = false)
        => ViewSwitcherLayout.Plan(new WidgetArea(width, height), count, iconOnly);

    // ---- the ladder at the seven size tiers (three views, header hidden) -------------------

    [Fact]
    public void S1653_XS_stacks_named_buttons()
    {
        var plan = Plan(120, 120);

        Assert.True(plan.Vertical);
        Assert.Equal(SwitcherFace.Named, plan.Face);
        Assert.False(plan.Overflow);
    }

    [Fact]
    public void S1653_S_stacks_named_buttons_rather_than_a_ragged_grid()
    {
        var plan = Plan(200, 150);

        Assert.True(plan.Vertical);
        Assert.Equal(SwitcherFace.Named, plan.Face);
    }

    [Fact]
    public void S1653_M_stacks_buttons_with_the_widget_count()
    {
        var plan = Plan(320, 240);

        Assert.True(plan.Vertical);
        Assert.Equal(SwitcherFace.Detailed, plan.Face);
    }

    [Fact]
    public void S1653_a_wide_strip_is_one_row_of_view_cards()
    {
        var plan = Plan(480, 160);

        Assert.Equal((3, 1), (plan.Columns, plan.Rows));
        Assert.Equal(SwitcherFace.Card, plan.Face);
    }

    [Fact]
    public void S1653_a_tall_column_stacks_view_cards()
    {
        var plan = Plan(200, 400);

        Assert.True(plan.Vertical);
        Assert.Equal(SwitcherFace.Card, plan.Face);
    }

    [Theory]
    [InlineData(560, 420)]
    [InlineData(800, 560)]
    public void S1653_L_and_XL_are_a_grid_of_cards_whose_last_row_widens(double width, double height)
    {
        var plan = Plan(width, height);

        Assert.Equal((2, 2), (plan.Columns, plan.Rows));
        Assert.Equal(SwitcherFace.Card, plan.Face);
        Assert.Equal(1, plan.CellsInRow(1));

        var top = ViewSwitcherLayout.Cell(new WidgetArea(width, height), plan, 0);
        var bottom = ViewSwitcherLayout.Cell(new WidgetArea(width, height), plan, 1);
        Assert.True(bottom.Width > top.Width * 1.9, "the last row's one card takes the whole width");
        Assert.Equal(CardShape.Above, ViewSwitcherLayout.CardShapeFor(top));
        Assert.Equal(CardShape.Beside, ViewSwitcherLayout.CardShapeFor(bottom));
    }

    [Fact]
    public void S1653_the_default_strip_is_one_row_with_names()
    {
        var plan = Plan(320, 56);

        Assert.Equal((3, 1), (plan.Columns, plan.Rows));
        Assert.Equal(SwitcherFace.Named, plan.Face);
    }

    [Fact]
    public void S1653_a_strip_too_narrow_for_names_keeps_initials()
    {
        var plan = Plan(240, 60);

        Assert.Equal((3, 1), (plan.Columns, plan.Rows));
        Assert.Equal(SwitcherFace.Mark, plan.Face);
    }

    [Theory]
    [InlineData(560, 420)]
    [InlineData(200, 400)]
    [InlineData(320, 56)]
    public void S1653_initials_only_never_shows_more_than_the_mark(double width, double height)
        => Assert.Equal(SwitcherFace.Mark, Plan(width, height, iconOnly: true).Face);

    [Fact]
    public void S1653_every_cell_of_a_plan_carries_its_face()
    {
        for (var width = 60; width <= 900; width += 37)
        {
            for (var height = 40; height <= 700; height += 41)
            {
                var area = new WidgetArea(width, height);
                var plan = ViewSwitcherLayout.Plan(area, 5, iconOnly: false);

                for (var row = 0; row < plan.Rows; row++)
                {
                    var face = ViewSwitcherLayout.FaceFor(ViewSwitcherLayout.Cell(area, plan, row), iconOnly: false);
                    Assert.True(face >= plan.Face, $"{width}x{height} row {row}: {face} < {plan.Face}");
                }

                Assert.Equal(plan.Rows == 0 ? 0 : plan.Cells, Enumerable.Range(0, plan.Rows).Sum(plan.CellsInRow));
            }
        }
    }

    [Fact]
    public void S1653_no_views_or_no_size_draws_nothing()
    {
        Assert.Equal(0, Plan(320, 240, count: 0).Cells);
        Assert.Equal(0, ViewSwitcherLayout.Plan(new WidgetArea(double.NaN, 100), 3, false).Cells);
    }

    // ---- overflow ------------------------------------------------------------------------------

    [Fact]
    public void S1655_twelve_views_on_XS_show_eight_and_a_plus_cell()
    {
        var plan = Plan(120, 120, count: 12);

        Assert.True(plan.Overflow);
        Assert.Equal((3, 3, 9), (plan.Columns, plan.Rows, plan.Cells));
        Assert.Equal(8, plan.Views);
    }

    [Fact]
    public void S1655_a_box_smaller_than_one_cell_still_offers_the_menu()
    {
        var plan = Plan(30, 30, count: 4);

        Assert.True(plan.Overflow);
        Assert.Equal(1, plan.Cells);
        Assert.Equal(0, plan.Views);
    }

    [Fact]
    public void S1655_a_crowded_large_tile_fits_every_view_without_overflow()
    {
        var plan = Plan(800, 560, count: 12);

        Assert.False(plan.Overflow);
        Assert.Equal(12, plan.Views);
    }

    // ---- keyboard ------------------------------------------------------------------------------

    private static readonly SwitcherLayout Grid = new(2, 2, 3, SwitcherFace.Card, false);
    private static readonly SwitcherLayout Column = new(1, 3, 3, SwitcherFace.Named, false);

    [Fact]
    public void S1652_the_first_arrow_lands_on_the_shown_view()
    {
        Assert.Equal(new SwitcherCursor(1, true), ViewSwitcherLayout.Step(Column, null, 1, WidgetKey.Down));
        Assert.Equal(new SwitcherCursor(1, true), ViewSwitcherLayout.Step(Column, null, 1, WidgetKey.Up));
    }

    [Fact]
    public void S1652_without_a_shown_cell_the_first_arrow_takes_the_end_it_points_to()
    {
        Assert.Equal(0, ViewSwitcherLayout.Step(Column, null, null, WidgetKey.Right).Index);
        Assert.Equal(2, ViewSwitcherLayout.Step(Column, null, null, WidgetKey.Left).Index);
    }

    [Fact]
    public void S1652_a_single_column_or_row_steps_one_cell_and_clamps()
    {
        Assert.Equal(2, ViewSwitcherLayout.Step(Column, 1, 0, WidgetKey.Down).Index);
        Assert.Equal(2, ViewSwitcherLayout.Step(Column, 2, 0, WidgetKey.Down).Index);
        Assert.Equal(0, ViewSwitcherLayout.Step(Column, 0, 0, WidgetKey.Up).Index);
        Assert.Equal(1, ViewSwitcherLayout.Step(Column, 0, 0, WidgetKey.Right).Index);
    }

    [Fact]
    public void S1652_a_grid_steps_a_row_with_up_and_down()
    {
        Assert.Equal(2, ViewSwitcherLayout.Step(Grid, 0, 0, WidgetKey.Down).Index);
        Assert.Equal(2, ViewSwitcherLayout.Step(Grid, 1, 0, WidgetKey.Down).Index); // into the shorter last row
        Assert.Equal(2, ViewSwitcherLayout.Step(Grid, 2, 0, WidgetKey.Down).Index);
        Assert.Equal(0, ViewSwitcherLayout.Step(Grid, 2, 0, WidgetKey.Up).Index);
        Assert.Equal(1, ViewSwitcherLayout.Step(Grid, 1, 0, WidgetKey.Up).Index);
        Assert.Equal(1, ViewSwitcherLayout.Step(Grid, 0, 0, WidgetKey.Right).Index);
    }

    [Fact]
    public void S1652_home_end_and_escape()
    {
        Assert.Equal(new SwitcherCursor(0, true), ViewSwitcherLayout.Step(Grid, 2, 0, WidgetKey.Home));
        Assert.Equal(new SwitcherCursor(2, true), ViewSwitcherLayout.Step(Grid, null, 0, WidgetKey.End));
        Assert.Equal(new SwitcherCursor(null, true), ViewSwitcherLayout.Step(Grid, 1, 0, WidgetKey.Escape));
        Assert.Equal(new SwitcherCursor(null, false), ViewSwitcherLayout.Step(Grid, null, 0, WidgetKey.Escape));
    }

    [Fact]
    public void S1652_enter_space_and_menu_act_only_on_a_cursor()
    {
        foreach (var key in new[] { WidgetKey.Enter, WidgetKey.Space, WidgetKey.Menu })
        {
            Assert.Equal(new SwitcherCursor(1, true), ViewSwitcherLayout.Step(Grid, 1, 0, key));
            Assert.Equal(new SwitcherCursor(null, false), ViewSwitcherLayout.Step(Grid, null, 0, key));
        }
    }

    [Fact]
    public void S1652_other_keys_and_a_stale_cursor()
    {
        Assert.Equal(new SwitcherCursor(1, false), ViewSwitcherLayout.Step(Grid, 1, 0, WidgetKey.Delete));
        Assert.Equal(new SwitcherCursor(0, true), ViewSwitcherLayout.Step(Grid, 9, 0, WidgetKey.Down)); // gone: starts again
        Assert.Equal(new SwitcherCursor(null, false), ViewSwitcherLayout.Step(new SwitcherLayout(1, 0, 0, SwitcherFace.Mark, false), null, null, WidgetKey.Down));
    }

    // ---- sketches ------------------------------------------------------------------------------

    [Fact]
    public void S1656_the_screen_fits_the_box_at_its_own_shape()
    {
        var wide = ViewSwitcherLayout.Sketch([], 160, 160, 16.0 / 9.0);
        Assert.Equal(160, wide.Width, 6);
        Assert.Equal(90, wide.Height, 6);

        var short_ = ViewSwitcherLayout.Sketch([], 100, 20, 2);
        Assert.Equal(40, short_.Width, 6);
        Assert.Equal(20, short_.Height, 6);
    }

    [Fact]
    public void S1656_tiles_scale_to_the_screen_and_are_cut_to_it()
    {
        var sketch = ViewSwitcherLayout.Sketch([new FracRect(0.5, 0.5, 0.25, 0.5), new FracRect(-0.2, 0.9, 0.4, 0.4)], 200, 100, 2);

        Assert.Equal(2, sketch.Tiles.Count);
        Assert.Equal(new SketchTile(100, 50, 50, 50), sketch.Tiles[0]);
        Assert.Equal(0, sketch.Tiles[1].X, 6);
        Assert.Equal(40, sketch.Tiles[1].Width, 6);
        Assert.Equal(10, sketch.Tiles[1].Height, 6);
    }

    [Fact]
    public void S1656_off_screen_empty_and_broken_tiles_are_left_out()
    {
        var sketch = ViewSwitcherLayout.Sketch(
            [new FracRect(1.2, 0, 0.3, 0.3), new FracRect(0.1, 0.1, 0, 0.3), new FracRect(double.NaN, 0, 0.2, 0.2)],
            200, 100, 2);

        Assert.Empty(sketch.Tiles);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void S1656_an_unknown_screen_shape_is_16_by_9(double aspect)
    {
        var sketch = ViewSwitcherLayout.Sketch([], 160, 160, aspect);

        Assert.Equal(90, sketch.Height, 6);
    }

    [Fact]
    public void S1656_no_room_no_sketch()
        => Assert.Same(ViewSketch.Empty, ViewSwitcherLayout.Sketch([new FracRect(0, 0, 1, 1)], 0, 50, 2));
}
