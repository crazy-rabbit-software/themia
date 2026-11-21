using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-13400]..[S-13404] The view bar's styles: dots, Previous / Next, one button; stepping and the wheel.</summary>
public sealed class ViewBarStyleTests
{
    private static readonly DipRect Area = new(0, 0, 960, 492);

    private static readonly string[] Four = ["a", "b", "c", "d"];

    [Theory]
    [InlineData("a", 1, "b")]
    [InlineData("c", 1, "d")]
    [InlineData("d", 1, "a")]
    [InlineData("a", -1, "d")]
    [InlineData("b", -1, "a")]
    [InlineData("b", 6, "d")]
    [InlineData("b", -6, "d")]
    public void S13404_steps_round_from_the_last_view_to_the_first_and_back(string shown, int delta, string expected)
        => Assert.Equal(expected, ViewBarRules.Step(Four, shown, delta));

    [Fact]
    public void S13404_a_display_showing_a_view_off_the_bar_steps_onto_its_first_or_last()
    {
        Assert.Equal("a", ViewBarRules.Step(Four, "elsewhere", 1));
        Assert.Equal("d", ViewBarRules.Step(Four, "elsewhere", -1));
        Assert.Equal("b", ViewBarRules.Step(Four, null, 2));
        Assert.Equal("c", ViewBarRules.Step(Four, null, -2));
    }

    [Fact]
    public void S13404_there_is_nowhere_to_step_with_no_view_one_shown_view_or_no_delta()
    {
        Assert.Null(ViewBarRules.Step([], "a", 1));
        Assert.Null(ViewBarRules.Step(["a"], "a", 1));
        Assert.Null(ViewBarRules.Step(["a"], "a", -3));
        Assert.Null(ViewBarRules.Step(Four, "a", 0));
        Assert.Null(ViewBarRules.Step(Four, "a", 4));
        Assert.Equal("a", ViewBarRules.Step(["a"], "other", 1));
    }

    [Fact]
    public void S13404_stepping_skips_the_views_the_bar_leaves_out_and_keeps_the_users_order()
    {
        var state = new AppState { Views = [] };
        foreach (var id in new[] { "a", "b", "c", "d" })
        {
            var view = ViewState.Create(id.ToUpperInvariant());
            view.Id = id;
            state.Views.Add(view);
        }

        var bar = new ViewBarSettings { VisibleViews = ["d", "b"] };
        var onBar = ViewBarRules.VisibleViews(state.Views, bar).Select(v => v.Id).ToList();

        Assert.Equal(["b", "d"], onBar);
        Assert.Equal("d", ViewBarRules.Step(onBar, "b", 1));
        Assert.Equal("b", ViewBarRules.Step(onBar, "d", 1));
        Assert.Equal("b", ViewBarRules.Step(onBar, "c", 1));
    }

    [Fact]
    public void S13404_a_wheel_notch_towards_the_user_is_next_and_away_is_previous()
    {
        Assert.Equal((1, 0d), ViewBarRules.Wheel(0, -ViewBarRules.WheelNotch));
        Assert.Equal((-1, 0d), ViewBarRules.Wheel(0, ViewBarRules.WheelNotch));
        Assert.Equal((-3, 0d), ViewBarRules.Wheel(0, 3 * ViewBarRules.WheelNotch));
    }

    [Fact]
    public void S13404_a_touchpads_small_deltas_add_up_to_a_notch_first()
    {
        var (steps, kept) = ViewBarRules.Wheel(0, -40);
        Assert.Equal((0, -40d), (steps, kept));
        (steps, kept) = ViewBarRules.Wheel(kept, -40);
        Assert.Equal((0, -80d), (steps, kept));
        (steps, kept) = ViewBarRules.Wheel(kept, -50);
        Assert.Equal((1, -10d), (steps, kept));
    }

    [Fact]
    public void S13404_turning_the_other_way_starts_again_from_nothing()
    {
        var (steps, kept) = ViewBarRules.Wheel(-100, 60);
        Assert.Equal((0, 60d), (steps, kept));
        Assert.Equal((0, 5d), ViewBarRules.Wheel(5, 0));
        Assert.Equal((0, 0d), ViewBarRules.Wheel(double.NaN, double.NaN));
    }

    [Fact]
    public void S13403_the_one_button_says_where_the_shown_view_stands_on_the_bar()
    {
        Assert.Equal((2, 4), ViewBarRules.Position(Four, "c"));
        Assert.Null(ViewBarRules.Position(Four, "elsewhere"));
        Assert.Null(ViewBarRules.Position(Four, null));
        Assert.Null(ViewBarRules.Position([], "a"));
    }

    [Theory]
    [InlineData(ViewBarType.Inline, true)]
    [InlineData(ViewBarType.Dots, true)]
    [InlineData(ViewBarType.Select, false)]
    [InlineData(ViewBarType.Arrows, false)]
    [InlineData(ViewBarType.Cycle, false)]
    public void S13400_chip_styles_stack_at_the_side_anchors_the_others_are_rows(ViewBarType type, bool chips)
    {
        Assert.Equal(chips, ViewBarRules.HasChips(type));
        Assert.Equal(chips, ViewBarRules.IsVertical(ViewBarAnchor.LeftCenter, type));
        Assert.Equal(chips, ViewBarRules.IsVertical(ViewBarAnchor.RightCenter, type));
        Assert.False(ViewBarRules.IsVertical(ViewBarAnchor.BottomLeft, type));
        if (!chips)
        {
            Assert.Equal(960 - (2 * ViewBarRules.EdgeMargin), ViewBarRules.Room(Area, ViewBarAnchor.LeftCenter, null, type));
        }
    }

    [Fact]
    public void S13400_the_one_view_styles_menu_lists_every_view_the_dots_overflow_the_left_out()
    {
        var fit = new ViewBarFit([0, 1, 4], Overflow: true);

        Assert.Equal([0, 1, 2, 3, 4], ViewBarRules.MenuViews(ViewBarType.Arrows, 5, fit));
        Assert.Equal([0, 1, 2, 3, 4], ViewBarRules.MenuViews(ViewBarType.Cycle, 5, fit));
        Assert.Equal([2, 3], ViewBarRules.MenuViews(ViewBarType.Dots, 5, fit));
    }

    [Fact]
    public void S13401_only_the_dots_leave_the_labels_unused()
    {
        Assert.Equal(
            [ViewBarType.Dots],
            Enum.GetValues<ViewBarType>().Where(t => !ViewBarRules.UsesLabels(t)));
    }

    [Theory]
    [InlineData(ViewBarType.Dots)]
    [InlineData(ViewBarType.Arrows)]
    [InlineData(ViewBarType.Cycle)]
    public void S13400_each_style_round_trips_through_the_state_file(ViewBarType type)
    {
        var state = new AppState { Views = [] };
        var view = ViewState.Create("A");
        view.Id = "a";
        state.Views.Add(view);
        state.ActiveViewId = "a";
        ViewBarRules.Update(state, "m1", bar => bar.Type = type);

        var json = System.Text.Json.JsonSerializer.Serialize(state, StateJson.AppState);
        var parsed = StateDocument.Parse(json);

        Assert.NotNull(parsed.State);
        Assert.Equal(type, parsed.State!.ViewBars["m1"].Type);
        Assert.Equal(type, parsed.State.ViewBars["m1"].Clone().Type);
    }
}
