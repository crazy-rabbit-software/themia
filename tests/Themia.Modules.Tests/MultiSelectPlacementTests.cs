using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>Multi-select and the one placement session ([S-1580]..[S-1593]).</summary>
public sealed class MultiSelectPlacementTests
{
    private static readonly ScreenMetrics Screen = new(1000, 1000);

    private static WidgetInstance At(WidgetId kind, string id, double x, double y, double w, double h)
    {
        var widget = WidgetInstance.Create(kind, new FracRect(x, y, w, h));
        widget.Id = id;
        return widget;
    }

    private static ViewState View(params WidgetInstance[] widgets)
    {
        var view = ViewState.Create("v");
        view.Widgets.AddRange(widgets);
        return view;
    }

    // ---- [S-1580] the selection ----------------------------------------------------------------

    [Fact]
    public void S1580_ctrl_click_toggles_and_the_last_picked_is_primary()
    {
        var selection = new WidgetSelection();

        Assert.True(selection.Toggle("v", "a"));
        Assert.True(selection.Toggle("v", "b"));
        Assert.Equal(["a", "b"], selection.Ids);
        Assert.Equal("b", selection.Primary);

        Assert.True(selection.Toggle("v", "a"));
        Assert.Equal(["b"], selection.Ids);

        Assert.True(selection.Toggle("v", "b"));
        Assert.True(selection.IsEmpty);
        Assert.Null(selection.ViewId);
    }

    [Fact]
    public void S1580_picking_in_another_view_starts_over_rather_than_mixing_views()
    {
        var selection = new WidgetSelection();
        selection.Replace("left", ["a", "b"]);

        selection.Toggle("right", "c");

        Assert.Equal("right", selection.ViewId);
        Assert.Equal(["c"], selection.Ids);
    }

    [Fact]
    public void S1580_a_plain_click_selects_exactly_one_and_null_clears()
    {
        var selection = new WidgetSelection();
        selection.Replace("v", ["a", "b"]);

        Assert.True(selection.Set("v", "b"));
        Assert.Equal(["b"], selection.Ids);
        Assert.False(selection.Set("v", "b"));

        Assert.True(selection.Set(null, null));
        Assert.True(selection.IsEmpty);
        Assert.False(selection.Clear());
    }

    [Fact]
    public void S1580_replace_drops_duplicates_and_reports_no_change_for_the_same_set()
    {
        var selection = new WidgetSelection();

        Assert.True(selection.Replace("v", ["a", "a", "b", ""]));
        Assert.Equal(["a", "b"], selection.Ids);
        Assert.False(selection.Replace("v", ["a", "b"]));
    }

    [Fact]
    public void S1583_pruning_drops_deleted_widgets_and_a_vanished_view()
    {
        var state = AppState.CreateDefault();
        var view = state.ActiveView();
        view.Widgets.Clear();
        view.Widgets.AddRange([At(new WidgetId("clock"), "a", 0, 0, 0.1, 0.1), At(new WidgetId("cpu"), "b", 0.2, 0, 0.1, 0.1)]);
        var selection = new WidgetSelection();
        selection.Replace(view.Id, ["a", "b"]);

        view.Widgets.RemoveAll(w => w.Id == "a");
        Assert.True(selection.Prune(state));
        Assert.Equal(["b"], selection.Ids);
        Assert.False(selection.Prune(state));

        state.Views.Clear();
        Assert.True(selection.Prune(state));
        Assert.True(selection.IsEmpty);
    }

    // ---- [S-1581] / [S-1582] rubber band and Ctrl+A -----------------------------------------------

    [Fact]
    public void S1581_the_band_selects_every_widget_it_touches_in_view_order()
    {
        var view = View(
            At(new WidgetId("clock"), "a", 0.1, 0.1, 0.1, 0.1),
            At(new WidgetId("cpu"), "b", 0.5, 0.5, 0.1, 0.1),
            At(new WidgetId("memory"), "c", 0.18, 0.3, 0.1, 0.1));

        // Touches a (corner) and c (edge), not b.
        var hits = SelectionRules.Lasso(view, new FracRect(0.15, 0.15, 0.1, 0.2));

        Assert.Equal(["a", "c"], hits);
    }

    [Fact]
    public void S1581_the_band_skips_hidden_widgets()
    {
        var view = View(
            At(new WidgetId("cpu"), "shown", 0.2, 0.2, 0.1, 0.1),
            At(new WidgetId("memory"), "hidden", 0.05, 0.05, 0.05, 0.05));

        var hits = SelectionRules.Lasso(view, new FracRect(0, 0, 0.45, 0.45), new HashSet<string> { "hidden" });

        Assert.Equal(["shown"], hits);
    }

    [Fact]
    public void S1581_a_band_of_no_area_touches_nothing()
    {
        var view = View(At(new WidgetId("clock"), "a", 0.1, 0.1, 0.1, 0.1));

        Assert.Empty(SelectionRules.Lasso(view, new FracRect(0.15, 0.15, 0, 0)));
        Assert.Empty(SelectionRules.Lasso(view, new FracRect(double.NaN, 0, 1, 1)));
    }

    [Fact]
    public void S1582_select_all_takes_every_widget_but_the_hidden_ones()
    {
        var view = View(
            At(new WidgetId("clock"), "t", 0.5, 0.5, 0.3, 0.3),
            At(new WidgetId("memory"), "hidden", 0.1, 0.1, 0.1, 0.1),
            At(new WidgetId("cpu"), "c", 0, 0, 0.1, 0.1));

        Assert.Equal(["t", "c"], SelectionRules.All(view, new HashSet<string> { "hidden" }));
    }

    [Fact]
    public void S1588_the_picked_widgets_come_in_view_order_and_unknown_ids_are_ignored()
    {
        var view = View(
            At(new WidgetId("clock"), "a", 0, 0, 0.1, 0.1),
            At(new WidgetId("cpu"), "b", 0.3, 0.3, 0.1, 0.1),
            At(new WidgetId("memory"), "c", 0.7, 0.7, 0.1, 0.1));

        Assert.Equal(["a", "c"], SelectionRules.InView(view, ["c", "missing", "a"]));
    }

    // ---- [S-1584] block geometry ------------------------------------------------------------------

    [Fact]
    public void S1584_a_block_moves_by_one_translation_keeping_offsets()
    {
        FracRect[] start = [new(0.1, 0.1, 0.1, 0.1), new(0.3, 0.2, 0.2, 0.1)];

        var delta = BlockGeometry.Move(start, 0.05, 0.1, Screen);
        var moved = start.Select(r => BlockGeometry.Apply(r, delta, Screen)).ToList();

        Assert.Equal(0.15, moved[0].X, 9);
        Assert.Equal(0.35, moved[1].X, 9);
        Assert.Equal(moved[1].X - moved[0].X, start[1].X - start[0].X, 9);
        Assert.Equal(moved[1].Y - moved[0].Y, start[1].Y - start[0].Y, 9);
        Assert.Equal(0.2, moved[1].W, 9);
    }

    [Fact]
    public void S1584_S6608_a_block_is_stopped_as_a_whole_so_offsets_survive_the_edge()
    {
        FracRect[] start = [new(0.6, 0.1, 0.1, 0.1), new(0.8, 0.1, 0.1, 0.1)];

        // Far past the right edge: the block stops when its leading widget keeps the visible minimum.
        var delta = BlockGeometry.Move(start, 0.5, 0, Screen);
        var moved = start.Select(r => BlockGeometry.Apply(r, delta, Screen)).ToList();

        Assert.Equal(1 - 0.032, moved[1].X, 9);
        Assert.Equal(0.2, moved[1].X - moved[0].X, 9);
    }

    [Fact]
    public void S1584_S6608_S855_a_block_goes_over_a_taskbar_until_a_widget_would_keep_less_than_the_visible_minimum()
    {
        // Owner, 2026-11-21: "the widgets should be able to go over the screen": a taskbar's edge is no stop.
        var screen = new ScreenMetrics(1000, 1000) { Allowed = new FracRect(0, 0, 1, 0.9) };
        FracRect[] start = [new(0.1, 0.5, 0.1, 0.1), new(0.1, 0.7, 0.1, 0.1)];

        var delta = BlockGeometry.Move(start, 0, 0.5, screen);

        Assert.Equal(0.9 - 0.032, BlockGeometry.Apply(start[1], delta, screen).Y, 9);
        Assert.Equal(0.7 - 0.032, BlockGeometry.Apply(start[0], delta, screen).Y, 9);
    }

    [Fact]
    public void S1584_a_block_snaps_at_its_corner_and_no_widget_changes_size()
    {
        // 1000 DIPs → 100 snap cells of 0.01.
        FracRect[] start = [new(0.1, 0.1, 0.123, 0.1), new(0.3, 0.1, 0.1, 0.1)];

        var delta = BlockGeometry.Move(start, 0.0042, 0.0061, Screen);
        var moved = start.Select(r => BlockGeometry.Apply(r, delta, Screen)).ToList();

        Assert.Equal(0.10, moved[0].X, 9);
        Assert.Equal(0.11, moved[0].Y, 9);
        Assert.Equal(0.123, moved[0].W, 9);
        Assert.Equal(0.2, moved[1].X - moved[0].X, 9);
    }

    [Fact]
    public void S1584_S6608_a_block_wider_than_the_screen_moves_until_one_widget_would_leave_reach()
    {
        FracRect[] start = [new(0.0, 0.0, 0.6, 0.2), new(0.7, 0.0, 0.6, 0.2)];

        var right = BlockGeometry.Move(start, 0.3, 0, Screen);
        var left = BlockGeometry.Move(start, -1, 0, Screen);

        // Rightwards the right widget stops first, leftwards the left one; the offset never changes.
        Assert.Equal(1 - 0.032, BlockGeometry.Apply(start[1], right, Screen).X, 9);
        Assert.Equal(0.032, BlockGeometry.Apply(start[0], left, Screen).Right, 9);
        Assert.Equal(0.7, BlockGeometry.Apply(start[1], left, Screen).X - BlockGeometry.Apply(start[0], left, Screen).X, 9);
    }

    [Fact]
    public void S1584_non_finite_input_does_not_move_anything()
    {
        var delta = BlockGeometry.Move([new FracRect(0.1, 0.1, 0.1, 0.1)], double.NaN, 0, Screen);

        Assert.Equal(default, delta);
    }

    // ---- [S-1585] / [S-1586] the payload's ghost and landing ---------------------------------------

    [Fact]
    public void S1586_a_payload_is_centred_on_the_pointer_as_a_block()
    {
        var view = View(At(new WidgetId("clock"), "a", 0.1, 0.1, 0.1, 0.1), At(new WidgetId("cpu"), "b", 0.3, 0.1, 0.1, 0.1));
        var payload = PlacementPayload.FromView(view, ["a", "b"], PlacementMode.Copy)!;

        var outlines = payload.Outlines(0.5, 0.5, Screen);

        // Bounding box 0.1..0.4 x 0.1..0.2, centred on (0.5, 0.5).
        Assert.Equal(0.35, outlines[0].Rect.X, 9);
        Assert.Equal(0.55, outlines[1].Rect.X, 9);
        Assert.Equal(0.45, outlines[0].Rect.Y, 9);
    }

    [Fact]
    public void S1586_a_hand_off_centres_the_held_widget_on_the_pointer()
    {
        var view = View(At(new WidgetId("clock"), "a", 0.1, 0.1, 0.1, 0.1), At(new WidgetId("cpu"), "b", 0.3, 0.1, 0.1, 0.1));
        var payload = PlacementPayload.FromView(view, ["a", "b"], PlacementMode.Move, anchorId: "b")!;

        var plan = payload.Plan(0.5, 0.5, Screen);

        var b = plan.Added.Single(w => w.Id == "b");
        Assert.Equal(0.5, b.X + (b.W / 2), 9);
        Assert.Equal(0.25, plan.Added.Single(w => w.Id == "a").X, 9);
    }

    [Fact]
    public void S1585_the_ghost_is_exactly_where_the_plan_lands()
    {
        var view = View(
            At(new WidgetId("clock"), "a", 0.1, 0.1, 0.3, 0.3),
            At(new WidgetId("cpu"), "c", 0.5, 0.1, 0.1, 0.1));
        var payload = PlacementPayload.FromView(view, ["a", "c"], PlacementMode.Copy)!;

        var outlines = payload.Outlines(0.6, 0.7, Screen);
        var plan = payload.Plan(0.6, 0.7, Screen);

        Assert.Equal([new WidgetId("clock"), new WidgetId("cpu")], outlines.Select(o => o.Kind));
        Assert.Equal(plan.Added.Single(w => w.Kind == new WidgetId("clock")).Rect, outlines[0].Rect);
        Assert.Equal(plan.Added.Single(w => w.Kind == new WidgetId("cpu")).Rect, outlines[1].Rect);
    }

    [Fact]
    public void S13708_a_drag_off_the_unplaced_page_hands_each_icons_config_to_its_ghost_so_it_is_drawn_as_on_the_desktop()
    {
        // Owner, 2026-11-20: "when dragging a desktop item from the topbar of non placed items it should already look like
        // the ones on the desktop". The placement as the host builds it: one item widget per icon, laid as one block.
        string[] keys = [@"C:\Users\a\Desktop\a.txt", @"C:\Users\a\Desktop\b.txt", @"C:\Users\a\Desktop\c.txt"];
        var block = Themia.Core.Desktop.UnplacedItems.Block(keys.Length, (0.08, 0.125));
        var holder = new ViewState
        {
            Id = "desktop-item",
            Name = string.Empty,
            Widgets = [.. keys.Select((key, i) =>
            {
                var widget = WidgetInstance.Create(new WidgetId("desktopItem"), block[i]);
                ((IDesktopItemConfig)widget.Config).Item = key;
                return widget;
            })],
        };
        var payload = PlacementPayload.FromView(holder, [.. holder.Widgets.Select(w => w.Id)], PlacementMode.Copy)!;

        var outlines = payload.Outlines(0.5, 0.5, Screen);
        var plan = payload.Plan(0.5, 0.5, Screen);

        // Each outline carries its icon's config (the ghost draws it as the desktop draws that icon), where that icon lands.
        Assert.Equal(keys, outlines.Select(o => Assert.IsAssignableFrom<IDesktopItemConfig>(o.Config).Item));
        Assert.Equal(plan.Added.Select(w => w.Rect), outlines.Select(o => o.Rect));

        // A new widget's ghost stays a frame: it has no config to draw.
        Assert.Null(Assert.Single(PlacementPayload.ForNew(new WidgetId("clock"), 200, 100).Outlines(0.5, 0.5, Screen)).Config);
    }

    [Fact]
    public void S13710_the_hint_names_unplaced_icons_by_their_own_names_and_never_as_copies()
    {
        static PlacementPayload Icons(params string[] names)
        {
            var holder = new ViewState
            {
                Id = "desktop-item",
                Name = string.Empty,
                Widgets = [.. names.Select((name, i) =>
                {
                    var widget = WidgetInstance.Create(new WidgetId("desktopItem"), new FracRect(i * 0.08, 0, 0.08, 0.125));
                    ((IDesktopItemConfig)widget.Config).Item = @"C:\Users\a\Desktop\" + name;
                    return widget;
                })],
            };
            return PlacementPayload.ForIcons(holder, names)!;
        }

        // One by its own name, several by their count: placing an unplaced icon on a view copies no file.
        Assert.Equal("Click to place Report.docx", PlacementHint.Verb(Icons("Report.docx")));
        Assert.Equal("Click to place 2 icons", PlacementHint.Verb(Icons("Report.docx", "Budget.xlsx")));
        Assert.Equal(["Report.docx", "Budget.xlsx"], Icons("Report.docx", "Budget.xlsx").IconNames);

        // Every other placement is worded as before: a copy of widgets is still a copy.
        var view = View(At(new WidgetId("clock"), "a", 0.1, 0.1, 0.1, 0.1), At(new WidgetId("cpu"), "b", 0.3, 0.1, 0.1, 0.1));
        var copyOne = PlacementPayload.FromView(view, ["a"], PlacementMode.Copy)!;
        Assert.Empty(copyOne.IconNames);
        Assert.Equal(Str.Shell_Place_CopyOne.Format(("name", ModuleCatalog.Of(new WidgetId("clock")).Name)), PlacementHint.Verb(copyOne));
        Assert.Equal(Str.Shell_Place_CopyMany.Plural(2), PlacementHint.Verb(PlacementPayload.FromView(view, ["a", "b"], PlacementMode.Copy)!));
        Assert.Equal(Str.Shell_Place_PlaceOne.Format(("name", ModuleCatalog.Of(new WidgetId("clock")).Name)), PlacementHint.Verb(PlacementPayload.ForNew(new WidgetId("clock"), 200, 100)));
    }

    [Fact]
    public void S1587_a_new_widget_payload_lands_one_widget_of_its_kind_centred_and_counts_one()
    {
        var payload = PlacementPayload.ForNew(new WidgetId("clock"), 200, 100);

        var plan = payload.Plan(0.5, 0.5, Screen);
        var widget = Assert.Single(plan.Added);

        Assert.Equal(new WidgetId("clock"), widget.Kind);
        Assert.Equal(new FracRect(0.4, 0.45, 0.2, 0.1), widget.Rect);
        Assert.Equal([widget.Id], plan.Roots);
        Assert.Empty(plan.RemoveIds);
        Assert.Equal(1, payload.Creates);
        Assert.Equal(new WidgetId("clock"), payload.SingleKind);
    }

    [Fact]
    public void S1587_S855_a_new_widget_on_the_taskbar_lands_wholly_above_it()
    {
        var screen = new ScreenMetrics(1000, 1000) { Allowed = new FracRect(0, 0, 1, 0.95) };

        var rect = PlacementPayload.ForNew(new WidgetId("clock"), 200, 100).Plan(0.5, 0.99, screen).Added[0].Rect;

        Assert.Equal(0.95 - 0.1, rect.Y, 9);
        Assert.Equal(0.1, rect.H, 9);
    }

    [Fact]
    public void S1587_only_creating_payloads_count_against_the_limit_before_the_ghost_appears()
    {
        var view = View(At(new WidgetId("cpu"), "g", 0, 0, 0.5, 0.5), At(new WidgetId("clock"), "m", 0.6, 0.6, 0.1, 0.1));

        Assert.Equal(2, PlacementPayload.FromView(view, ["g", "m"], PlacementMode.Copy)!.Creates);
        Assert.Equal(0, PlacementPayload.FromView(view, ["g", "m"], PlacementMode.Move)!.Creates);
    }

    [Fact]
    public void S1588_a_payload_holds_the_picked_widgets_in_view_order()
    {
        var view = View(
            At(new WidgetId("cpu"), "g", 0, 0, 0.5, 0.5),
            At(new WidgetId("clock"), "m", 0.6, 0.1, 0.1, 0.1),
            At(new WidgetId("memory"), "other", 0.7, 0.7, 0.1, 0.1));

        var payload = PlacementPayload.FromView(view, ["m", "g"], PlacementMode.Copy)!;

        Assert.Equal(["g", "m"], payload.RootIds);
        Assert.Equal(["g", "m"], payload.Widgets.Select(w => w.Id));
        Assert.Equal(2, payload.Count);
        Assert.True(payload.Holds("m"));
        Assert.False(payload.Holds("other"));
        Assert.Null(PlacementPayload.FromView(view, ["missing"], PlacementMode.Copy));
    }

    [Fact]
    public void S1589_every_landing_of_a_copy_gets_fresh_ids()
    {
        var view = View(At(new WidgetId("clock"), "m", 0.1, 0.1, 0.1, 0.1));
        var payload = PlacementPayload.FromView(view, ["m"], PlacementMode.Copy)!;

        var first = payload.Plan(0.5, 0.5, Screen).Added.Single();
        var second = payload.Plan(0.5, 0.5, Screen).Added.Single();

        Assert.NotEqual("m", first.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void S1590_a_move_keeps_ids()
    {
        var view = View(
            At(new WidgetId("cpu"), "s", 0.1, 0.1, 0.3, 0.3),
            At(new WidgetId("clock"), "c", 0.5, 0.1, 0.1, 0.1));
        var plan = PlacementPayload.FromView(view, ["s", "c"], PlacementMode.Move)!.Plan(0.5, 0.5, Screen);

        Assert.Equal(["s", "c"], plan.Added.Select(w => w.Id));
        Assert.Equal(["s", "c"], plan.RemoveIds);
        Assert.Equal(["s", "c"], plan.Roots);
    }

    [Fact]
    public void S1590_a_move_across_views_lands_the_whole_selection_once()
    {
        var state = AppState.CreateDefault();
        var left = state.ActiveView();
        left.Widgets.Clear();
        var right = ViewState.Create("right");
        state.Views.Add(right);
        left.Widgets.AddRange([At(new WidgetId("clock"), "a", 0.1, 0.1, 0.1, 0.1), At(new WidgetId("cpu"), "b", 0.3, 0.1, 0.1, 0.1), At(new WidgetId("memory"), "stay", 0.6, 0.6, 0.1, 0.1)]);

        var plan = PlacementPayload.FromView(left, ["a", "b"], PlacementMode.Move)!.Plan(0.5, 0.5, Screen);
        Assert.Equal(0, plan.NetAdded(state));
        state.ApplyPlacement(plan, right.Id);

        Assert.Equal(["stay"], left.Widgets.Select(w => w.Id));
        Assert.Equal(["a", "b"], right.Widgets.Select(w => w.Id));
        Assert.Equal(0.2, right.Find("b")!.X - right.Find("a")!.X, 9);
    }
}
