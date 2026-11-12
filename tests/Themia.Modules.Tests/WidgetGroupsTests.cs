using Themia.Core.Geometry;
using Themia.Core.Mcp;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2450]..[S-2458] Groups: an optional lock over widgets of one view.</summary>
public sealed class WidgetGroupsTests
{
    private static readonly ScreenMetrics Screen = new(1000, 1000);

    private static WidgetInstance At(string kind, string id, double x, double y, double w = 0.1, double h = 0.1, string? group = null)
    {
        var widget = WidgetInstance.Create(new WidgetId(kind), new FracRect(x, y, w, h));
        widget.Id = id;
        widget.Group = group;
        return widget;
    }

    /// <summary>a, b in group g ("Pair"); c and d lone.</summary>
    private static ViewState Sample()
    {
        var view = ViewState.Create("v");
        view.Id = "v";
        view.Widgets.AddRange([
            At("cpu", "a", 0.1, 0.1, group: "g"),
            At("memory", "b", 0.2, 0.1, group: "g"),
            At("clock", "c", 0.5, 0.5),
            At("disk", "d", 0.7, 0.1),
        ]);
        view.Groups.Add(new WidgetGroup { Id = "g", Name = "Pair" });
        return view;
    }

    private static AppState State(params ViewState[] views) => new() { Views = [.. views], ActiveViewId = views[0].Id };

    // ---- [S-2450] state ------------------------------------------------------------------------

    [Fact]
    public void S2450_a_group_round_trips_through_the_state_file_and_a_lone_widget_writes_no_group()
    {
        var json = SettingsTransfer.Serialize(State(Sample()));
        var read = AppStateStore.Parse(json);

        Assert.Empty(read.Repairs);
        var view = read.State!.Views[0];
        Assert.Equal("Pair", Assert.Single(view.Groups).Name);
        Assert.Equal(["a", "b"], WidgetGroups.Members(view, "g").Select(w => w.Id));
        Assert.Null(view.Find("c")!.Group);
        Assert.DoesNotContain("\"group\": null", json, StringComparison.Ordinal);
    }

    [Fact]
    public void S2450_load_dissolves_a_group_of_one_and_lets_go_of_a_group_that_is_not_there()
    {
        var view = Sample();
        view.Find("b")!.Group = null;
        view.Find("c")!.Group = "missing";
        view.Groups.Add(new WidgetGroup { Id = "g", Name = "again" });
        view.Groups.Add(new WidgetGroup { Id = " ", Name = "blank" });

        var read = AppStateStore.Parse(SettingsTransfer.Serialize(State(view)));

        var loaded = read.State!.Views[0];
        Assert.Empty(loaded.Groups);
        Assert.All(loaded.Widgets, w => Assert.Null(w.Group));
        Assert.Contains(read.Repairs, r => r.Contains("fewer than two", StringComparison.Ordinal));
        Assert.Contains(read.Repairs, r => r.Contains("does not have", StringComparison.Ordinal));
        Assert.Contains(read.Repairs, r => r.Contains("repeated id", StringComparison.Ordinal));
    }

    [Fact]
    public void S2450_a_blank_name_is_no_name()
    {
        var view = Sample();
        view.Groups[0].Name = "   ";

        var repairs = WidgetGroups.Repair(view);

        Assert.Null(view.Groups[0].Name);
        Assert.Single(repairs);
    }

    [Fact]
    public void S2450_a_delete_that_leaves_one_member_dissolves_the_group()
    {
        var state = State(Sample());
        state.Views[0].Widgets.RemoveAll(w => w.Id == "b");

        WidgetGroups.Settle(state);

        Assert.Empty(state.Views[0].Groups);
        Assert.Null(state.Views[0].Find("a")!.Group);
    }

    [Fact]
    public void S2450_the_assistants_list_shows_each_widgets_group_and_the_views_groups()
    {
        var desk = McpDesk.Of(State(Sample()), []);

        var view = Assert.Single(desk.Views);
        Assert.Equal(new McpGroup("g", "Pair"), Assert.Single(view.Groups));
        Assert.Equal("g", view.Widgets.Single(w => w.Id == "a").Group);
        Assert.Null(view.Widgets.Single(w => w.Id == "c").Group);
    }

    // ---- [S-2454] group, ungroup, leave ----------------------------------------------------------

    [Fact]
    public void S2454_grouping_takes_whole_groups_merges_them_and_keeps_the_first_name()
    {
        var view = Sample();

        var group = WidgetGroups.Group(view, ["b", "c"]);

        Assert.NotNull(group);
        Assert.Equal("Pair", group.Name);
        Assert.Equal(["a", "b", "c"], WidgetGroups.Members(view, group.Id).Select(w => w.Id));
        Assert.Equal(group, Assert.Single(view.Groups));
    }

    [Fact]
    public void S2454_one_widget_or_exactly_one_group_does_not_group()
    {
        var view = Sample();

        Assert.False(WidgetGroups.CanGroup(view, ["c"]));
        Assert.False(WidgetGroups.CanGroup(view, ["a", "b"]));
        Assert.False(WidgetGroups.CanGroup(view, ["a"]));
        Assert.True(WidgetGroups.CanGroup(view, ["c", "d"]));
        Assert.Null(WidgetGroups.Group(view, ["a"]));
    }

    [Fact]
    public void S2454_ungroup_dissolves_and_leaving_takes_one_member_out()
    {
        var view = Sample();
        var trio = WidgetGroups.Group(view, ["a", "c", "d"])!;

        Assert.True(WidgetGroups.RemoveFromGroup(view, "c"));
        Assert.Equal(["a", "b", "d"], WidgetGroups.Members(view, trio.Id).Select(w => w.Id));

        Assert.True(WidgetGroups.Ungroup(view, ["d"]));
        Assert.Empty(view.Groups);
        Assert.All(view.Widgets, w => Assert.Null(w.Group));
        Assert.False(WidgetGroups.CanUngroup(view, ["a"]));
    }

    [Fact]
    public void S2454_leaving_a_pair_dissolves_it()
    {
        var view = Sample();

        WidgetGroups.RemoveFromGroup(view, "a");

        Assert.Empty(view.Groups);
        Assert.Null(view.Find("b")!.Group);
    }

    // ---- [S-2456] undo -----------------------------------------------------------------------------

    [Fact]
    public void S2456_grouping_is_an_undo_step_and_undo_restores_the_groups()
    {
        var state = State(Sample());
        var history = new LayoutHistory(state);

        WidgetGroups.Ungroup(state.Views[0], ["a"]);
        Assert.True(history.Commit(state, UndoAction.Ungroup));

        Assert.Equal(UndoAction.Ungroup, history.Undo(state));
        var view = state.Views[0];
        Assert.Equal("Pair", Assert.Single(view.Groups).Name);
        Assert.Equal("g", view.Find("a")!.Group);

        Assert.Equal(UndoAction.Ungroup, history.Redo(state));
        Assert.Empty(state.Views[0].Groups);
    }

    [Fact]
    public void S2456_renaming_a_group_is_a_layout_change()
    {
        var state = State(Sample());
        var before = LayoutShape.Capture(state);
        state.Views[0].Groups[0].Name = "Other";

        Assert.False(LayoutShape.Capture(state).SameLayout(before));
    }

    // ---- [S-2455] copy, move, duplicate ---------------------------------------------------------

    [Fact]
    public void S2455_a_copied_group_lands_as_a_new_group_with_the_same_name_every_time()
    {
        var state = State(Sample());
        var payload = PlacementPayload.FromView(state.Views[0], ["a", "b"], PlacementMode.Copy)!;

        var first = payload.Plan(0.5, 0.5, Screen, snapEnabled: false);
        state.ApplyPlacement(first, "v");
        var second = payload.Plan(0.3, 0.8, Screen, snapEnabled: false);
        state.ApplyPlacement(second, "v");

        var view = state.Views[0];
        Assert.Equal(3, view.Groups.Count);
        Assert.All(view.Groups, g => Assert.Equal("Pair", g.Name));
        Assert.Equal(3, view.Groups.Select(g => g.Id).Distinct().Count());
        Assert.All(view.Groups, g => Assert.Equal(2, WidgetGroups.Members(view, g.Id).Count));
    }

    [Fact]
    public void S2455_a_member_copied_alone_lands_as_a_lone_widget()
    {
        var state = State(Sample());
        var payload = PlacementPayload.FromView(state.Views[0], ["a"], PlacementMode.Copy)!;

        var plan = payload.Plan(0.5, 0.8, Screen, snapEnabled: false);
        state.ApplyPlacement(plan, "v");

        Assert.Null(state.Views[0].Find(plan.Roots[0])!.Group);
        Assert.Single(state.Views[0].Groups);
    }

    [Fact]
    public void S2455_a_group_moved_to_another_view_keeps_its_group_and_the_old_view_lets_it_go()
    {
        var other = ViewState.Create("other");
        var state = State(Sample(), other);
        var payload = PlacementPayload.FromView(state.Views[0], ["a", "b"], PlacementMode.Move)!;

        state.ApplyPlacement(payload.Plan(0.5, 0.5, Screen, snapEnabled: false), other.Id);

        Assert.Empty(state.Views[0].Groups);
        var moved = Assert.Single(other.Groups);
        Assert.Equal(("g", "Pair"), (moved.Id, moved.Name));
        Assert.Equal(["a", "b"], WidgetGroups.Members(other, "g").Select(w => w.Id));
    }

    [Fact]
    public void S2455_a_duplicated_view_has_new_groups_of_its_copies()
    {
        var state = State(Sample());

        var copy = ViewOps.Duplicate(state, "v")!;

        var group = Assert.Single(copy.Groups);
        Assert.NotEqual("g", group.Id);
        Assert.Equal("Pair", group.Name);
        Assert.Equal(2, WidgetGroups.Members(copy, group.Id).Count);
        Assert.Equal("g", state.Views[0].Find("a")!.Group);
    }

    [Fact]
    public void S2455_a_copied_group_counts_each_widget_for_the_licence()
    {
        var payload = PlacementPayload.FromView(Sample(), ["a", "b"], PlacementMode.Copy)!;

        Assert.Equal(2, payload.Creates);
    }

    // ---- [S-2451] selection -----------------------------------------------------------------------

    [Fact]
    public void S2451_a_click_on_a_member_selects_the_group_and_a_second_click_enters_it()
    {
        var view = Sample();
        var selection = new WidgetSelection();

        Assert.True(selection.Press(view, "a"));
        Assert.Equal(["a", "b"], selection.Ids);
        Assert.Null(selection.Entered);

        // The second press keeps the group (a drag would carry it); released without moving, it enters.
        Assert.False(selection.Press(view, "b"));
        Assert.True(selection.Click(view, "b"));
        Assert.Equal(["b"], selection.Ids);
        Assert.Equal("g", selection.Entered);

        // Inside, a press on another member enters that one.
        Assert.True(selection.Press(view, "a"));
        Assert.Equal(["a"], selection.Ids);
        Assert.Equal("g", selection.Entered);
    }

    [Fact]
    public void S2451_escape_steps_out_member_to_group_to_nothing()
    {
        var view = Sample();
        var selection = new WidgetSelection();
        selection.Enter("v", "g", "a");

        Assert.True(selection.StepOut(view));
        Assert.Equal(["a", "b"], selection.Ids);
        Assert.Null(selection.Entered);

        Assert.True(selection.StepOut(view));
        Assert.True(selection.IsEmpty);
    }

    [Fact]
    public void S2451_a_press_outside_the_entered_group_selects_that_unit_and_leaves_the_group()
    {
        var view = Sample();
        var selection = new WidgetSelection();
        selection.Enter("v", "g", "a");

        Assert.True(selection.Press(view, "c"));
        Assert.Equal(["c"], selection.Ids);
        Assert.Null(selection.Entered);
    }

    [Fact]
    public void S2451_a_click_on_one_unit_of_a_larger_selection_narrows_to_that_unit()
    {
        var view = Sample();
        var selection = new WidgetSelection();
        selection.Replace("v", ["a", "b", "c"]);

        Assert.False(selection.Press(view, "a"));
        Assert.True(selection.Click(view, "a"));
        Assert.Equal(["a", "b"], selection.Ids);
        Assert.Null(selection.Entered);
    }

    [Fact]
    public void S2451_ctrl_click_toggles_whole_groups()
    {
        var view = Sample();
        var selection = new WidgetSelection();
        selection.Replace("v", ["c"]);

        Assert.True(selection.ToggleUnit(view, "b"));
        Assert.Equal(["c", "a", "b"], selection.Ids.OrderBy(i => i == "c" ? 0 : 1).ThenBy(i => i));
        Assert.True(selection.ToggleUnit(view, "a"));
        Assert.Equal(["c"], selection.Ids);
    }

    [Fact]
    public void S2451_the_lasso_takes_a_group_when_it_touches_one_member()
    {
        var view = Sample();

        var hits = SelectionRules.Lasso(view, new FracRect(0.05, 0.05, 0.1, 0.1));

        Assert.Equal(["a", "b"], hits);
    }

    [Fact]
    public void S2451_tab_cycles_units_and_inside_an_entered_group_its_members()
    {
        var view = Sample();
        var selection = new WidgetSelection();

        Assert.True(selection.Cycle(view, backwards: false));
        Assert.Equal(["a", "b"], selection.Ids);
        Assert.True(selection.Cycle(view, backwards: false));
        Assert.Equal(["d"], selection.Ids);
        Assert.True(selection.Cycle(view, backwards: false));
        Assert.Equal(["c"], selection.Ids);

        selection.Enter("v", "g", "a");
        Assert.True(selection.Cycle(view, backwards: false));
        Assert.Equal(["b"], selection.Ids);
        Assert.Equal("g", selection.Entered);
        Assert.True(selection.Cycle(view, backwards: false));
        Assert.Equal(["a"], selection.Ids);
    }

    [Fact]
    public void S2451_an_entered_group_that_dissolves_is_entered_no_more()
    {
        var view = Sample();
        var state = State(view);
        var selection = new WidgetSelection();
        selection.Enter("v", "g", "a");

        WidgetGroups.Ungroup(view, ["a"]);

        Assert.True(selection.Prune(state));
        Assert.Null(selection.Entered);
        Assert.Equal(["a"], selection.Ids);
    }

    [Fact]
    public void S2453_the_frame_of_a_selected_group_is_its_members_union()
    {
        var view = Sample();

        var bounds = WidgetGroups.Bounds(view, "g")!.Value;
        Assert.Equal(0.1, bounds.X, 9);
        Assert.Equal(0.3, bounds.Right, 9);
        Assert.Equal(0.2, bounds.Bottom, 9);
        Assert.Equal(["g"], WidgetGroups.Whole(view, ["a", "b", "c"]).Select(g => g.Id));
        Assert.Empty(WidgetGroups.Whole(view, ["a", "c"]));
    }
}
