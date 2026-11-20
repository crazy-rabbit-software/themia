using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-2450] There are no groups (owner, 2026-11-17: "actually remove the group feature ... you should keep the ability to
/// select multiple widgets of course and also the ability to move them together or configure them together"): the
/// state has no group key, a selection is widgets, several selected move and are set together.
/// </summary>
public sealed class NoGroupsTests
{
    private static readonly ScreenMetrics Screen = new(1000, 1000);

    private static WidgetInstance At(string kind, string id, double x, double y, double w = 0.1, double h = 0.1)
    {
        var widget = WidgetInstance.Create(new WidgetId(kind), new FracRect(x, y, w, h));
        widget.Id = id;
        return widget;
    }

    /// <summary>a and b touch (edge to edge), c and d apart.</summary>
    private static ViewState Sample()
    {
        var view = ViewState.Create("v");
        view.Id = "v";
        view.Widgets.AddRange([At("cpu", "a", 0.1, 0.1), At("memory", "b", 0.2, 0.1), At("clock", "c", 0.5, 0.5), At("disk", "d", 0.7, 0.1)]);
        return view;
    }

    private static AppState State(params ViewState[] views) => new() { Views = [.. views], ActiveViewId = views[0].Id };

    // ---- the state ------------------------------------------------------------------------------

    [Fact]
    public void S2450_the_state_file_has_no_group_key()
    {
        var json = SettingsTransfer.Serialize(State(Sample()));

        Assert.DoesNotContain("\"group\":", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"groups\":", json, StringComparison.Ordinal);
        Assert.Empty(StateDocument.Parse(json).Repairs);
    }

    [Fact]
    public void S1050_a_groups_key_left_in_a_file_is_dropped_loudly_and_the_widgets_load()
    {
        // The schema changed in place: a file that still names groups loads, the keys gone, one line each.
        var root = System.Text.Json.Nodes.JsonNode.Parse(SettingsTransfer.Serialize(State(Sample())))!;
        var view = root["views"]![0]!.AsObject();
        view["groups"] = System.Text.Json.Nodes.JsonNode.Parse("""[ { "id": "g", "name": "Pair" } ]""");
        view["widgets"]![0]!.AsObject()["group"] = "g";

        var read = StateDocument.Parse(root.ToJsonString());

        Assert.NotNull(read.State);
        Assert.Equal(4, read.State!.Views[0].Widgets.Count);
        Assert.Contains(read.Repairs, r => r.Contains("unknown key 'groups'", StringComparison.Ordinal));
        Assert.Contains(read.Repairs, r => r.Contains("'a'", StringComparison.Ordinal) && r.Contains("unknown key 'group'", StringComparison.Ordinal));
    }

    // ---- selecting several ----------------------------------------------------------------------

    [Fact]
    public void S1593_a_press_selects_the_widget_alone_even_one_that_touches_another()
    {
        var view = Sample();
        var selection = new WidgetSelection();

        Assert.True(selection.Press(view, "a"));
        Assert.Equal(["a"], selection.Ids);
    }

    [Fact]
    public void S1593_a_press_on_a_selected_widget_keeps_the_selection_and_its_click_narrows_to_it()
    {
        var view = Sample();
        var selection = new WidgetSelection();
        selection.Replace("v", ["a", "b", "c"]);

        Assert.False(selection.Press(view, "b"));
        Assert.Equal(["a", "b", "c"], selection.Ids);
        Assert.True(selection.Click(view, "b"));
        Assert.Equal(["b"], selection.Ids);
    }

    [Fact]
    public void S1580_ctrl_click_adds_and_takes_out_one_widget()
    {
        var selection = new WidgetSelection();
        selection.Replace("v", ["c"]);

        Assert.True(selection.Toggle("v", "a"));
        Assert.Equal(["c", "a"], selection.Ids);
        Assert.True(selection.Toggle("v", "c"));
        Assert.Equal(["a"], selection.Ids);
    }

    [Fact]
    public void S1581_the_lasso_takes_what_it_touches_and_no_neighbour()
        => Assert.Equal(["a"], SelectionRules.Lasso(Sample(), new FracRect(0.12, 0.12, 0.02, 0.02)));

    [Fact]
    public void S1973_tab_cycles_widgets_one_by_one_and_escape_clears()
    {
        var view = Sample();
        var selection = new WidgetSelection();

        Assert.True(selection.Cycle(view, backwards: false));
        Assert.Equal(["a"], selection.Ids);
        Assert.True(selection.Cycle(view, backwards: false));
        Assert.Equal(["b"], selection.Ids); // the next in reading order, alone: touching "a" makes no unit of the two
        Assert.True(selection.Clear());
        Assert.True(selection.IsEmpty);
    }

    [Fact]
    public void S1584_several_selected_move_as_one_block_by_one_translation()
    {
        var view = Sample();
        var rects = SelectionRules.InView(view, ["c", "a"]).Select(id => view.Find(id)!.Rect).ToList();

        var delta = BlockGeometry.Move(rects, 0.05, 0.1, Screen);
        var moved = rects.Select(r => BlockGeometry.Apply(r, delta, Screen)).ToList();

        Assert.Equal(rects[1].X - rects[0].X, moved[1].X - moved[0].X, 9);
        Assert.Equal(rects[1].Y - rects[0].Y, moved[1].Y - moved[0].Y, 9);
    }

    [Fact]
    public void S1588_widgets_copied_together_land_as_loose_widgets()
    {
        var state = State(Sample());
        var payload = PlacementPayload.FromView(state.Views[0], ["a", "b"], PlacementMode.Copy)!;

        var plan = payload.Plan(0.5, 0.8, Screen);
        state.ApplyPlacement(plan, "v");

        Assert.Equal(6, state.Views[0].Widgets.Count);
        Assert.DoesNotContain("\"group\":", SettingsTransfer.Serialize(state), StringComparison.Ordinal);
    }

    [Fact]
    public void S900_a_duplicated_view_copies_its_widgets_and_nothing_else()
    {
        var state = State(Sample());

        var copy = ViewOps.Duplicate(state, "v")!;

        Assert.Equal(4, copy.Widgets.Count);
        Assert.Empty(copy.Widgets.Select(w => w.Id).Intersect(state.Views[0].Widgets.Select(w => w.Id)));
    }
}
